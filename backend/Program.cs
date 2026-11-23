using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using ControleFinanceiro.Data;
using ControleFinanceiro.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0))));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

// Login com cookie: depois do login, o navegador manda o cookie em toda requisição.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "controle_financeiro_sessao";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        // Numa API, quem não está logado recebe 401 (e não um redirecionamento para uma página de login).
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// Contra força bruta: cada IP pode tentar entrar ou se cadastrar 5 vezes por minuto.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancelamento) => new ValueTask(
        context.HttpContext.Response.WriteAsJsonAsync("Muitas tentativas. Aguarde um minuto e tente de novo.", cancelamento));
    options.AddPolicy("login", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

const string MesInvalido = "Mês inválido. Use o formato AAAA-MM, por exemplo 2026-11.";
var hasher = new PasswordHasher<Usuario>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "API de controle financeiro no ar");

// Autenticação
app.MapPost("/auth/cadastro", async (Credenciais entrada, AppDbContext db, HttpContext http) =>
{
    var email = entrada.Email?.Trim().ToLowerInvariant() ?? "";
    if (!email.Contains('@') || email.Length > 256)
        return Results.BadRequest("Informe um e-mail válido.");
    if (entrada.Senha is null || entrada.Senha.Length < 6)
        return Results.BadRequest("A senha deve ter pelo menos 6 caracteres.");
    if (await db.Usuarios.AnyAsync(u => u.Email == email))
        return Results.Conflict("Este e-mail já está cadastrado.");

    var primeiroUsuario = !await db.Usuarios.AnyAsync();

    var usuario = new Usuario { Email = email };
    usuario.SenhaHash = hasher.HashPassword(usuario, entrada.Senha);
    db.Usuarios.Add(usuario);
    await db.SaveChangesAsync();

    // Os dados criados antes do login existir passam a ser do primeiro usuário.
    if (primeiroUsuario)
    {
        await db.Categorias.Where(c => c.UsuarioId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsuarioId, usuario.Id));
        await db.Transacoes.Where(t => t.UsuarioId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsuarioId, usuario.Id));
    }

    await Entrar(http, usuario);
    return Results.Created("/auth/eu", new { usuario.Id, usuario.Email });
}).RequireRateLimiting("login");

app.MapPost("/auth/login", async (Credenciais entrada, AppDbContext db, HttpContext http) =>
{
    var email = entrada.Email?.Trim().ToLowerInvariant() ?? "";
    var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

    if (usuario is null || entrada.Senha is null ||
        hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, entrada.Senha) == PasswordVerificationResult.Failed)
        return Results.Json("E-mail ou senha incorretos.", statusCode: StatusCodes.Status401Unauthorized);

    await Entrar(http, usuario);
    return Results.Ok(new { usuario.Id, usuario.Email });
}).RequireRateLimiting("login");

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

// Trocar a senha: precisa estar logado e informar a senha atual.
app.MapPost("/auth/trocar-senha", async (TrocaDeSenha entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuario = await db.Usuarios.FindAsync(UsuarioId(user));
    if (usuario is null)
        return Results.Unauthorized();

    if (entrada.SenhaAtual is null ||
        hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, entrada.SenhaAtual) == PasswordVerificationResult.Failed)
        return Results.BadRequest("Senha atual incorreta.");
    if (entrada.NovaSenha is null || entrada.NovaSenha.Length < 6)
        return Results.BadRequest("A nova senha deve ter pelo menos 6 caracteres.");
    if (entrada.NovaSenha == entrada.SenhaAtual)
        return Results.BadRequest("A nova senha deve ser diferente da atual.");

    usuario.SenhaHash = hasher.HashPassword(usuario, entrada.NovaSenha);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("login");

app.MapGet("/auth/eu", (ClaimsPrincipal user) =>
    new { Id = UsuarioId(user), Email = user.FindFirstValue(ClaimTypes.Email) })
    .RequireAuthorization();

// Daqui para baixo, todas as rotas exigem login.
var rotas = app.MapGroup("").RequireAuthorization();

// Categorias
rotas.MapGet("/categorias", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    return await db.Categorias
        .Where(c => c.UsuarioId == usuarioId)
        .OrderBy(c => c.Nome)
        .ToListAsync();
});

rotas.MapGet("/categorias/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    return await db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId) is Categoria categoria
        ? Results.Ok(categoria)
        : Results.NotFound();
});

rotas.MapPost("/categorias", async (Categoria entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var erro = ValidarCategoria(entrada);
    if (erro is not null)
        return Results.BadRequest(erro);

    var categoria = new Categoria { Nome = entrada.Nome.Trim(), MetaMensal = entrada.MetaMensal, UsuarioId = UsuarioId(user) };
    db.Categorias.Add(categoria);
    await db.SaveChangesAsync();
    return Results.Created($"/categorias/{categoria.Id}", categoria);
});

rotas.MapPut("/categorias/{id}", async (int id, Categoria entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var erro = ValidarCategoria(entrada);
    if (erro is not null)
        return Results.BadRequest(erro);

    var usuarioId = UsuarioId(user);
    var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (categoria is null)
        return Results.NotFound();

    categoria.Nome = entrada.Nome.Trim();
    categoria.MetaMensal = entrada.MetaMensal;
    await db.SaveChangesAsync();
    return Results.Ok(categoria);
});

rotas.MapDelete("/categorias/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (categoria is null)
        return Results.NotFound();

    if (await db.Transacoes.AnyAsync(t => t.CategoriaId == id))
        return Results.Conflict("Não é possível excluir uma categoria que possui transações.");

    db.Categorias.Remove(categoria);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Transações
rotas.MapGet("/transacoes", async (string? mes, ClaimsPrincipal user, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(DoUsuario(db.Transacoes.Include(t => t.Categoria), user), mes);
    if (consulta is null)
        return Results.BadRequest(MesInvalido);

    return Results.Ok(await consulta
        .OrderByDescending(t => t.Data)
        .ThenByDescending(t => t.Id)
        .ToListAsync());
});

rotas.MapGet("/transacoes/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
    await DoUsuario(db.Transacoes.Include(t => t.Categoria), user).FirstOrDefaultAsync(t => t.Id == id) is Transacao transacao
        ? Results.Ok(transacao)
        : Results.NotFound());

// ?repetir=12 cria a mesma transação em 12 meses seguidos (sem o parâmetro, cria só 1).
// ?parcelas=10 divide o valor total em 10 parcelas mensais: "Geladeira (1/10)" ... "(10/10)".
rotas.MapPost("/transacoes", async (Transacao entrada, string? repetir, string? parcelas, ClaimsPrincipal user, AppDbContext db) =>
{
    if (repetir is not null && parcelas is not null)
        return Results.BadRequest("Escolha repetir ou parcelar, não os dois.");

    var meses = 1;
    if (repetir is not null && (!int.TryParse(repetir, out meses) || meses < 1 || meses > 60))
        return Results.BadRequest("Repetir deve ser um número de meses entre 1 e 60.");

    var parcelado = parcelas is not null;
    if (parcelado && (!int.TryParse(parcelas, out meses) || meses < 2 || meses > 60))
        return Results.BadRequest("O número de parcelas deve ser entre 2 e 60.");

    var usuarioId = UsuarioId(user);
    var erro = await ValidarTransacao(entrada, usuarioId, db);
    if (erro is not null)
        return Results.BadRequest(erro);

    // Parcelas: todas com o mesmo valor, e a 1ª fica com os centavos que sobram
    // (R$ 100 em 3x = 33,34 + 33,33 + 33,33), para a soma dar exatamente o total.
    var total = Math.Round(entrada.Valor, 2);
    var valorParcela = parcelado ? Math.Floor(total * 100 / meses) / 100 : total;
    if (parcelado && valorParcela < 0.01m)
        return Results.BadRequest("O valor total é pequeno demais para tantas parcelas (cada uma precisa ter pelo menos R$ 0,01).");
    var primeiraParcela = parcelado ? total - valorParcela * (meses - 1) : total;

    // Repetindo ou parcelando, todas as cópias ganham a mesma marca de série.
    Guid? serieId = meses > 1 ? Guid.NewGuid() : null;

    var transacoes = Enumerable.Range(0, meses).Select(i =>
    {
        var copia = new Transacao { UsuarioId = usuarioId, SerieId = serieId };
        CopiarCampos(entrada, copia);
        // Sempre a partir da data original: 31/01 vira 28/02 (ou 29/02) e depois 31/03.
        copia.Data = entrada.Data.AddMonths(i);
        if (parcelado)
        {
            copia.Valor = i == 0 ? primeiraParcela : valorParcela;
            copia.Descricao = $"{copia.Descricao} ({i + 1}/{meses})";
        }
        return copia;
    }).ToList();

    // Um único SaveChanges: grava todas ou nenhuma.
    db.Transacoes.AddRange(transacoes);
    await db.SaveChangesAsync();

    var transacao = transacoes[0];
    await db.Entry(transacao).Reference(t => t.Categoria).LoadAsync();
    return Results.Created($"/transacoes/{transacao.Id}", transacao);
});

// ?seguintes=true também altera as transações seguintes da mesma série (menos a data).
rotas.MapPut("/transacoes/{id}", async (int id, Transacao entrada, bool? seguintes, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var transacao = await DoUsuario(db.Transacoes, user).FirstOrDefaultAsync(t => t.Id == id);
    if (transacao is null)
        return Results.NotFound();

    var erro = await ValidarTransacao(entrada, usuarioId, db);
    if (erro is not null)
        return Results.BadRequest(erro);

    var dataOriginal = transacao.Data;
    CopiarCampos(entrada, transacao);

    if (seguintes == true && transacao.SerieId is not null)
    {
        // Parcelas: cada uma mantém a própria numeração "(x/y)" no fim da descrição.
        var parcelaEditada = Regex.Match(transacao.Descricao, @"^(.*) \(\d+/\d+\)$");
        foreach (var proxima in await SeguintesDaSerie(db, user, transacao, dataOriginal))
        {
            var numeracao = Regex.Match(proxima.Descricao, @" \(\d+/\d+\)$");
            proxima.Descricao = parcelaEditada.Success && numeracao.Success
                ? parcelaEditada.Groups[1].Value + numeracao.Value
                : transacao.Descricao;
            proxima.Valor = transacao.Valor;
            proxima.Tipo = transacao.Tipo;
            proxima.CategoriaId = transacao.CategoriaId;
        }
    }

    // Um único SaveChanges: grava todas ou nenhuma.
    await db.SaveChangesAsync();
    await db.Entry(transacao).Reference(t => t.Categoria).LoadAsync();
    return Results.Ok(transacao);
});

// ?seguintes=true também exclui as transações seguintes da mesma série.
rotas.MapDelete("/transacoes/{id}", async (int id, bool? seguintes, ClaimsPrincipal user, AppDbContext db) =>
{
    var transacao = await DoUsuario(db.Transacoes, user).FirstOrDefaultAsync(t => t.Id == id);
    if (transacao is null)
        return Results.NotFound();

    db.Transacoes.Remove(transacao);
    if (seguintes == true && transacao.SerieId is not null)
        db.Transacoes.RemoveRange(await SeguintesDaSerie(db, user, transacao, transacao.Data));
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Saldo
rotas.MapGet("/saldo", async (string? mes, ClaimsPrincipal user, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(DoUsuario(db.Transacoes, user), mes);
    if (consulta is null)
        return Results.BadRequest(MesInvalido);

    var receitas = await consulta
        .Where(t => t.Tipo == TipoTransacao.Receita)
        .SumAsync(t => t.Valor);
    var despesas = await consulta
        .Where(t => t.Tipo == TipoTransacao.Despesa)
        .SumAsync(t => t.Valor);

    if (mes is null)
        return Results.Ok(new { receitas, despesas, saldo = receitas - despesas });

    // Com mês: também o acumulado, ou seja, o saldo de tudo até o fim desse mês.
    var fimDoMes = DateTime.ParseExact(mes, "yyyy-MM", null).AddMonths(1);
    var acumulado = await SaldoAntesDe(DoUsuario(db.Transacoes, user), fimDoMes);
    return Results.Ok(new { receitas, despesas, saldo = receitas - despesas, acumulado });
});

// Gráfico: total de despesas por categoria, do maior para o menor
rotas.MapGet("/despesas-por-categoria", async (string? mes, ClaimsPrincipal user, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(DoUsuario(db.Transacoes, user), mes);
    if (consulta is null)
        return Results.BadRequest(MesInvalido);

    var totais = await consulta
        .Where(t => t.Tipo == TipoTransacao.Despesa)
        .GroupBy(t => new { t.CategoriaId, t.Categoria!.Nome })
        .Select(g => new { categoriaId = g.Key.CategoriaId, categoria = g.Key.Nome, total = g.Sum(t => t.Valor) })
        .OrderByDescending(x => x.total)
        .ToListAsync();

    return Results.Ok(totais);
});

// Metas do mês: para cada categoria com meta, a meta e quanto já foi gasto no mês
rotas.MapGet("/metas", async (string? mes, ClaimsPrincipal user, AppDbContext db) =>
{
    if (mes is null)
        return Results.BadRequest("Informe o mês, por exemplo /metas?mes=2026-11.");
    var despesasDoMes = FiltrarPorMes(DoUsuario(db.Transacoes, user), mes);
    if (despesasDoMes is null)
        return Results.BadRequest(MesInvalido);
    despesasDoMes = despesasDoMes.Where(t => t.Tipo == TipoTransacao.Despesa);

    var usuarioId = UsuarioId(user);
    var metas = await db.Categorias
        .Where(c => c.UsuarioId == usuarioId && c.MetaMensal != null)
        .Select(c => new
        {
            categoriaId = c.Id,
            categoria = c.Nome,
            meta = c.MetaMensal!.Value,
            gasto = despesasDoMes.Where(t => t.CategoriaId == c.Id).Sum(t => t.Valor),
        })
        .ToListAsync();

    // As mais perto de estourar primeiro.
    return Results.Ok(metas.OrderByDescending(m => m.gasto / m.meta));
});

// Resumo mês a mês: receitas, despesas e saldo de cada mês do ano (meses sem transação vêm zerados)
rotas.MapGet("/resumo-mensal", async (string? ano, ClaimsPrincipal user, AppDbContext db) =>
{
    var anoEscolhido = DateTime.Today.Year;
    if (ano is not null && (!int.TryParse(ano, out anoEscolhido) || anoEscolhido < 1900 || anoEscolhido > 2100))
        return Results.BadRequest("Ano inválido. Use quatro dígitos, por exemplo 2026.");

    var inicio = new DateTime(anoEscolhido, 1, 1);
    var fim = inicio.AddYears(1);

    // O banco devolve um total por (mês, tipo); só vêm os meses que têm transações.
    var totais = await DoUsuario(db.Transacoes, user)
        .Where(t => t.Data >= inicio && t.Data < fim)
        .GroupBy(t => new { t.Data.Month, t.Tipo })
        .Select(g => new { Mes = g.Key.Month, g.Key.Tipo, Total = g.Sum(t => t.Valor) })
        .ToListAsync();

    // O acumulado começa com o que sobrou (ou faltou) dos anos anteriores.
    var acumulado = await SaldoAntesDe(DoUsuario(db.Transacoes, user), inicio);

    // Monta os 12 meses, preenchendo com zero os que não têm transações.
    var meses = Enumerable.Range(1, 12).Select(m =>
    {
        var receitas = totais.Where(x => x.Mes == m && x.Tipo == TipoTransacao.Receita).Sum(x => x.Total);
        var despesas = totais.Where(x => x.Mes == m && x.Tipo == TipoTransacao.Despesa).Sum(x => x.Total);
        acumulado += receitas - despesas;
        return new { mes = $"{anoEscolhido}-{m:00}", receitas, despesas, saldo = receitas - despesas, acumulado };
    }).ToList();

    return Results.Ok(meses);
});

app.Run();

// Id do usuário logado, guardado no cookie no momento do login.
static int UsuarioId(ClaimsPrincipal user) =>
    int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

// Só as transações do usuário logado.
static IQueryable<Transacao> DoUsuario(IQueryable<Transacao> consulta, ClaimsPrincipal user)
{
    var usuarioId = UsuarioId(user);
    return consulta.Where(t => t.UsuarioId == usuarioId);
}

// Receitas menos despesas de todas as transações antes da data.
static async Task<decimal> SaldoAntesDe(IQueryable<Transacao> consulta, DateTime data) =>
    await consulta
        .Where(t => t.Data < data)
        .SumAsync(t => t.Tipo == TipoTransacao.Receita ? t.Valor : -t.Valor);

// As outras transações da mesma série, da data informada em diante.
static async Task<List<Transacao>> SeguintesDaSerie(AppDbContext db, ClaimsPrincipal user, Transacao transacao, DateTime aPartirDe) =>
    await DoUsuario(db.Transacoes, user)
        .Where(t => t.SerieId == transacao.SerieId && t.Id != transacao.Id && t.Data >= aPartirDe)
        .ToListAsync();

// Cria o cookie de login.
static async Task Entrar(HttpContext http, Usuario usuario)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new Claim(ClaimTypes.Email, usuario.Email),
    };
    var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
}

static string? ValidarCategoria(Categoria entrada)
{
    if (string.IsNullOrWhiteSpace(entrada.Nome))
        return "O nome da categoria é obrigatório.";
    if (entrada.MetaMensal is <= 0)
        return "A meta mensal deve ser maior que zero (ou vazia, para não ter meta).";
    return null;
}

static async Task<string?> ValidarTransacao(Transacao entrada, int usuarioId, AppDbContext db)
{
    if (string.IsNullOrWhiteSpace(entrada.Descricao))
        return "A descrição é obrigatória.";
    if (entrada.Valor <= 0)
        return "O valor deve ser maior que zero.";
    if (entrada.Data == default)
        return "A data é obrigatória.";
    if (!Enum.IsDefined(entrada.Tipo))
        return "O tipo deve ser Receita ou Despesa.";
    if (!await db.Categorias.AnyAsync(c => c.Id == entrada.CategoriaId && c.UsuarioId == usuarioId))
        return "Categoria não encontrada.";
    return null;
}

// Filtra as transações do mês ("2026-11" = de 01/11/2026 até antes de 01/12/2026).
// Sem mês, devolve a consulta como está. Com mês inválido, devolve null.
static IQueryable<Transacao>? FiltrarPorMes(IQueryable<Transacao> consulta, string? mes)
{
    if (mes is null)
        return consulta;
    if (!DateTime.TryParseExact(mes, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var inicio))
        return null;

    var fim = inicio.AddMonths(1);
    return consulta.Where(t => t.Data >= inicio && t.Data < fim);
}

static void CopiarCampos(Transacao origem, Transacao destino)
{
    destino.Descricao = origem.Descricao.Trim();
    destino.Valor = origem.Valor;
    destino.Data = origem.Data;
    destino.Tipo = origem.Tipo;
    destino.CategoriaId = origem.CategoriaId;
}

record Credenciais(string? Email, string? Senha);
record TrocaDeSenha(string? SenhaAtual, string? NovaSenha);
