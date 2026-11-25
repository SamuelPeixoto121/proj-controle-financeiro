using System.Globalization;
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

// A conexão é lida na hora de usar (e não aqui em cima), para os testes automatizados
// conseguirem trocá-la pela do banco de testes.
builder.Services.AddDbContext<AppDbContext>((servicos, options) =>
    options.UseMySql(
        servicos.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0))));

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
        // Conta de visitante (site de demonstração): o login vale só até a conta ser apagada.
        options.Events.OnValidatePrincipal = context =>
        {
            var ate = context.Principal?.FindFirstValue("visitante_ate");
            if (ate is not null && DateTime.Parse(ate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) < DateTime.UtcNow)
                context.RejectPrincipal();
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
// (O número vem da configuração "LimiteLogin:PorMinuto", 5 se não houver; os testes automatizados aumentam.)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancelamento) => new ValueTask(
        context.HttpContext.Response.WriteAsJsonAsync("Muitas tentativas. Aguarde um minuto e tente de novo.", cancelamento));
    options.AddPolicy("login", http =>
    {
        var limite = http.RequestServices.GetRequiredService<IConfiguration>().GetValue("LimiteLogin:PorMinuto", 5);
        return RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1) });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

const string MesInvalido = "Mês inválido. Use o formato AAAA-MM, por exemplo 2026-11.";
var hasher = new PasswordHasher<Usuario>();

// Site de demonstração (publicado): cadastro fechado, botão "Entrar como visitante" com dados
// inventados e as tabelas criadas/atualizadas sozinhas ao ligar. No PC fica desligado.
var demo = app.Configuration.GetValue<bool>("Demo:Ativo");
if (demo)
{
    using var escopo = app.Services.CreateScope();
    escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// As telas do React (só no site publicado, que tem a pasta wwwroot; no PC elas vêm do Vite).
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/saude", () => "API de controle financeiro no ar");

// Diz à tela de login se este é o site de demonstração.
app.MapGet("/auth/modo", () => new { Demo = demo });

// Site de demonstração: cria uma conta de visitante, já com dados de exemplo, e entra com ela.
app.MapPost("/auth/visitante", async (AppDbContext db, HttpContext http) =>
{
    if (!demo)
        return Results.NotFound();

    await ApagarVisitantesVencidos(db);
    var usuario = new Usuario
    {
        Email = $"visitante-{Guid.NewGuid():N}@exemplo.com",
        VisitanteAte = DateTime.UtcNow.AddHours(24),
    };
    usuario.SenhaHash = hasher.HashPassword(usuario, Guid.NewGuid().ToString()); // ninguém sabe a senha
    db.Usuarios.Add(usuario);
    await db.SaveChangesAsync();
    await DadosDeExemplo.Criar(db, usuario.Id, DateTime.Today);

    await Entrar(http, usuario);
    return Results.Created("/auth/eu", new { usuario.Id, usuario.Email, Visitante = true });
}).RequireRateLimiting("login");

// Autenticação
app.MapPost("/auth/cadastro", async (Credenciais entrada, AppDbContext db, HttpContext http) =>
{
    if (demo)
        return Results.Json("Este é um site de demonstração: o cadastro está fechado. Use \"Entrar como visitante\".",
            statusCode: StatusCodes.Status403Forbidden);

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

    // Todo usuário começa com uma conta (dá para renomear e criar outras depois).
    var conta = new Conta { Nome = "Conta principal", UsuarioId = usuario.Id };
    db.Contas.Add(conta);
    await db.SaveChangesAsync();

    // Os dados criados antes do login existir passam a ser do primeiro usuário.
    if (primeiroUsuario)
    {
        await db.Categorias.Where(c => c.UsuarioId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsuarioId, usuario.Id));
        await db.Transacoes.Where(t => t.UsuarioId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsuarioId, usuario.Id).SetProperty(t => t.ContaId, conta.Id));
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
    return Results.Ok(new { usuario.Id, usuario.Email, Visitante = usuario.VisitanteAte is not null });
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
    new { Id = UsuarioId(user), Email = user.FindFirstValue(ClaimTypes.Email), Visitante = user.HasClaim(c => c.Type == "visitante_ate") })
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

// Cartões de crédito
rotas.MapGet("/cartoes", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    return await db.Cartoes.Where(c => c.UsuarioId == usuarioId).OrderBy(c => c.Nome).ToListAsync();
});

rotas.MapPost("/cartoes", async (Cartao entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var erro = ValidarCartao(entrada);
    if (erro is not null)
        return Results.BadRequest(erro);

    if (entrada.ContaId is not null && !await ContaDoUsuario(db, UsuarioId(user), entrada.ContaId.Value))
        return Results.BadRequest("Conta não encontrada.");

    var cartao = new Cartao
    {
        Nome = entrada.Nome.Trim(),
        DiaFechamento = entrada.DiaFechamento,
        DiaVencimento = entrada.DiaVencimento,
        ContaId = entrada.ContaId,
        UsuarioId = UsuarioId(user),
    };
    db.Cartoes.Add(cartao);
    await db.SaveChangesAsync();
    return Results.Created($"/cartoes/{cartao.Id}", cartao);
});

// Mudar os dias vale para as próximas compras; as já lançadas continuam nas faturas em que estão.
rotas.MapPut("/cartoes/{id}", async (int id, Cartao entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var erro = ValidarCartao(entrada);
    if (erro is not null)
        return Results.BadRequest(erro);

    var usuarioId = UsuarioId(user);
    var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (cartao is null)
        return Results.NotFound();

    if (entrada.ContaId is not null && !await ContaDoUsuario(db, usuarioId, entrada.ContaId.Value))
        return Results.BadRequest("Conta não encontrada.");

    cartao.Nome = entrada.Nome.Trim();
    cartao.DiaFechamento = entrada.DiaFechamento;
    cartao.DiaVencimento = entrada.DiaVencimento;
    cartao.ContaId = entrada.ContaId;
    await db.SaveChangesAsync();
    return Results.Ok(cartao);
});

rotas.MapDelete("/cartoes/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (cartao is null)
        return Results.NotFound();

    if (await db.Transacoes.AnyAsync(t => t.CartaoId == id))
        return Results.Conflict("Não é possível excluir um cartão que possui compras.");

    db.Cartoes.Remove(cartao);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Faturas do cartão: as compras agrupadas pelo vencimento, da mais antiga para a mais nova.
rotas.MapGet("/cartoes/{id}/faturas", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (cartao is null)
        return Results.NotFound();

    var compras = await db.Transacoes.Include(t => t.Categoria)
        .Where(t => t.CartaoId == id && t.UsuarioId == usuarioId)
        .OrderBy(t => t.DataCompra).ThenBy(t => t.Id)
        .ToListAsync();

    var hoje = DateTime.Today;
    return Results.Ok(compras.GroupBy(t => t.Data).OrderBy(g => g.Key).Select(g =>
    {
        var vencimento = g.Key;
        // A fatura fecha no mesmo mês do vencimento, ou no mês anterior se vence num dia menor.
        var mesDoFechamento = cartao.DiaVencimento > cartao.DiaFechamento ? vencimento : vencimento.AddMonths(-1);
        var fechamento = new DateTime(mesDoFechamento.Year, mesDoFechamento.Month, cartao.DiaFechamento);
        var paga = g.All(t => t.Pago);
        return new
        {
            vencimento,
            fechamento,
            total = g.Sum(t => t.Valor),
            situacao = paga ? "Paga" : vencimento < hoje ? "Atrasada" : hoje >= fechamento ? "Fechada" : "Aberta",
            compras = g.Select(t => new { t.Id, t.Descricao, t.Valor, t.DataCompra, categoria = t.Categoria?.Nome, t.Pago }),
        };
    }));
});

// Paga (ou desfaz o pagamento de) uma fatura inteira: todas as compras com esse vencimento.
rotas.MapPut("/cartoes/{id}/faturas/{vencimento}/pago", async (int id, string vencimento, Situacao entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    if (!DateTime.TryParseExact(vencimento, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var data))
        return Results.BadRequest("Vencimento inválido. Use o formato AAAA-MM-DD.");

    var usuarioId = UsuarioId(user);
    var compras = await db.Transacoes
        .Where(t => t.CartaoId == id && t.UsuarioId == usuarioId && t.Data == data)
        .ToListAsync();
    if (compras.Count == 0)
        return Results.NotFound();

    foreach (var compra in compras)
        compra.Pago = entrada.Pago;
    await db.SaveChangesAsync();
    return Results.Ok(new { compras = compras.Count });
});

// Meta de economia: quanto o usuário quer guardar por mês (vazio = sem meta).
rotas.MapGet("/meta-economia", async (ClaimsPrincipal user, AppDbContext db) =>
    await db.Usuarios.FindAsync(UsuarioId(user)) is Usuario usuario
        ? Results.Ok(new { metaMensal = usuario.MetaEconomiaMensal })
        : Results.Unauthorized());

rotas.MapPut("/meta-economia", async (MetaEconomia entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    if (entrada.MetaMensal is <= 0)
        return Results.BadRequest("A meta deve ser maior que zero (ou vazia, para não ter meta).");
    if (entrada.MetaMensal > 99_999_999.99m)
        return Results.BadRequest("A meta é grande demais.");

    var usuario = await db.Usuarios.FindAsync(UsuarioId(user));
    if (usuario is null)
        return Results.Unauthorized();

    usuario.MetaEconomiaMensal = entrada.MetaMensal;
    await db.SaveChangesAsync();
    return Results.Ok(new { metaMensal = usuario.MetaEconomiaMensal });
});

// Contas (banco, carteira, poupança...), com o saldo de cada uma:
// saldo inicial + receitas recebidas − despesas pagas + transferências que já aconteceram.
rotas.MapGet("/contas", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var hoje = DateTime.Today;
    var contas = await db.Contas.Where(c => c.UsuarioId == usuarioId).OrderBy(c => c.Id).ToListAsync();
    var movimento = await db.Transacoes
        .Where(t => t.UsuarioId == usuarioId && t.Pago && t.ContaId != null)
        .GroupBy(t => t.ContaId)
        .Select(g => new { ContaId = g.Key, Total = g.Sum(t => t.Tipo == TipoTransacao.Receita ? t.Valor : -t.Valor) })
        .ToListAsync();
    var transferencias = await db.Transferencias.Where(t => t.UsuarioId == usuarioId && t.Data <= hoje).ToListAsync();

    return contas.Select(c => new
    {
        c.Id,
        c.Nome,
        c.SaldoInicial,
        saldo = c.SaldoInicial
            + movimento.Where(m => m.ContaId == c.Id).Sum(m => m.Total)
            + transferencias.Where(t => t.ContaDestinoId == c.Id).Sum(t => t.Valor)
            - transferencias.Where(t => t.ContaOrigemId == c.Id).Sum(t => t.Valor),
    });
});

rotas.MapPost("/contas", async (Conta entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var erro = ValidarConta(entrada);
    if (erro is not null)
        return Results.BadRequest(erro);

    var conta = new Conta { Nome = entrada.Nome.Trim(), SaldoInicial = entrada.SaldoInicial, UsuarioId = UsuarioId(user) };
    db.Contas.Add(conta);
    await db.SaveChangesAsync();
    return Results.Created($"/contas/{conta.Id}", conta);
});

rotas.MapPut("/contas/{id}", async (int id, Conta entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var erro = ValidarConta(entrada);
    if (erro is not null)
        return Results.BadRequest(erro);

    var usuarioId = UsuarioId(user);
    var conta = await db.Contas.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (conta is null)
        return Results.NotFound();

    conta.Nome = entrada.Nome.Trim();
    conta.SaldoInicial = entrada.SaldoInicial;
    await db.SaveChangesAsync();
    return Results.Ok(conta);
});

rotas.MapDelete("/contas/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var conta = await db.Contas.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
    if (conta is null)
        return Results.NotFound();

    if (await db.Transacoes.AnyAsync(t => t.ContaId == id)
        || await db.Cartoes.AnyAsync(c => c.ContaId == id)
        || await db.Transferencias.AnyAsync(t => t.ContaOrigemId == id || t.ContaDestinoId == id))
        return Results.Conflict("Não é possível excluir uma conta que possui transações, transferências ou cartões.");
    if (await db.Contas.CountAsync(c => c.UsuarioId == usuarioId) == 1)
        return Results.Conflict("Não é possível excluir a última conta.");

    db.Contas.Remove(conta);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Transferências entre contas (não são receita nem despesa), da mais nova para a mais antiga.
rotas.MapGet("/transferencias", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var hoje = DateTime.Today;
    var nomes = await db.Contas.Where(c => c.UsuarioId == usuarioId).ToDictionaryAsync(c => c.Id, c => c.Nome);
    var lista = await db.Transferencias
        .Where(t => t.UsuarioId == usuarioId)
        .OrderByDescending(t => t.Data).ThenByDescending(t => t.Id)
        .ToListAsync();
    return lista.Select(t => new
    {
        t.Id,
        t.Descricao,
        t.Valor,
        t.Data,
        t.ContaOrigemId,
        t.ContaDestinoId,
        origem = nomes.GetValueOrDefault(t.ContaOrigemId),
        destino = nomes.GetValueOrDefault(t.ContaDestinoId),
        // Com data futura: ainda não mexeu no saldo das contas.
        agendada = t.Data > hoje,
    });
});

rotas.MapPost("/transferencias", async (Transferencia entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    if (entrada.Valor <= 0)
        return Results.BadRequest("O valor deve ser maior que zero.");
    if (entrada.Valor > 99_999_999.99m)
        return Results.BadRequest("O valor é grande demais.");
    if (entrada.Data == default)
        return Results.BadRequest("A data é obrigatória.");
    if (entrada.ContaOrigemId == entrada.ContaDestinoId)
        return Results.BadRequest("Escolha duas contas diferentes.");
    if (!await ContaDoUsuario(db, usuarioId, entrada.ContaOrigemId) || !await ContaDoUsuario(db, usuarioId, entrada.ContaDestinoId))
        return Results.BadRequest("Conta não encontrada.");

    var transferencia = new Transferencia
    {
        Descricao = entrada.Descricao?.Trim() ?? "",
        Valor = entrada.Valor,
        Data = entrada.Data,
        ContaOrigemId = entrada.ContaOrigemId,
        ContaDestinoId = entrada.ContaDestinoId,
        UsuarioId = usuarioId,
    };
    db.Transferencias.Add(transferencia);
    await db.SaveChangesAsync();
    return Results.Created($"/transferencias/{transferencia.Id}", transferencia);
});

rotas.MapDelete("/transferencias/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var usuarioId = UsuarioId(user);
    var transferencia = await db.Transferencias.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuarioId);
    if (transferencia is null)
        return Results.NotFound();

    db.Transferencias.Remove(transferencia);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Transações
rotas.MapGet("/transacoes", async (string? mes, ClaimsPrincipal user, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(DoUsuario(db.Transacoes.Include(t => t.Categoria).Include(t => t.Cartao).Include(t => t.Conta), user), mes);
    if (consulta is null)
        return Results.BadRequest(MesInvalido);

    return Results.Ok(await consulta
        .OrderByDescending(t => t.Data)
        .ThenByDescending(t => t.Id)
        .ToListAsync());
});

rotas.MapGet("/transacoes/{id}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
    await DoUsuario(db.Transacoes.Include(t => t.Categoria).Include(t => t.Cartao).Include(t => t.Conta), user).FirstOrDefaultAsync(t => t.Id == id) is Transacao transacao
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
    var cartao = entrada.CartaoId is null ? null : await db.Cartoes.FindAsync(entrada.CartaoId);
    // No cartão, a conta é a que paga a fatura; sem conta escolhida, a conta principal.
    var contaId = (cartao is not null ? cartao.ContaId : entrada.ContaId) ?? await ContaPrincipal(db, usuarioId);

    var transacoes = Enumerable.Range(0, meses).Select(i =>
    {
        var copia = new Transacao { UsuarioId = usuarioId, SerieId = serieId, ContaId = contaId };
        CopiarCampos(entrada, copia);
        // Sempre a partir da data original: 31/01 vira 28/02 (ou 29/02) e depois 31/03.
        copia.Data = entrada.Data.AddMonths(i);
        if (cartao is not null)
        {
            // No cartão, a data informada é a da compra.
            // Parcelar: uma compra só, uma parcela em cada fatura seguinte.
            // Repetir (ex.: assinatura): uma compra nova por mês.
            copia.DataCompra = parcelado ? entrada.Data : copia.Data;
            copia.Data = VencimentoDaFatura(cartao, copia.DataCompra.Value).AddMonths(parcelado ? i : 0);
        }
        // Só a 1ª segue o "já foi paga"; as dos meses seguintes começam pendentes.
        if (i > 0)
            copia.Pago = false;
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
    await db.Entry(transacao).Reference(t => t.Cartao).LoadAsync();
    await db.Entry(transacao).Reference(t => t.Conta).LoadAsync();
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
    // Uma parcela no cartão fica N faturas depois da fatura da compra (a 3ª parcela, 2 depois).
    // Ao recalcular a fatura (outra data ou outro cartão), mantém essa distância.
    var faturasDepois = 0;
    if (transacao.CartaoId is not null && transacao.DataCompra is not null
        && await db.Cartoes.FindAsync(transacao.CartaoId) is Cartao cartaoAntigo)
    {
        var faturaDaCompra = VencimentoDaFatura(cartaoAntigo, transacao.DataCompra.Value);
        faturasDepois = (dataOriginal.Year - faturaDaCompra.Year) * 12 + dataOriginal.Month - faturaDaCompra.Month;
    }

    CopiarCampos(entrada, transacao);
    transacao.DataCompra = null;
    if (transacao.CartaoId is not null && await db.Cartoes.FindAsync(transacao.CartaoId) is Cartao cartao)
    {
        transacao.DataCompra = entrada.Data;
        transacao.Data = VencimentoDaFatura(cartao, entrada.Data).AddMonths(faturasDepois);
        transacao.ContaId = cartao.ContaId ?? await ContaPrincipal(db, usuarioId);
    }
    else
    {
        // Sem conta informada, continua na mesma.
        transacao.ContaId = entrada.ContaId ?? transacao.ContaId ?? await ContaPrincipal(db, usuarioId);
    }

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
            proxima.ContaId = transacao.ContaId;
        }
    }

    // Um único SaveChanges: grava todas ou nenhuma.
    await db.SaveChangesAsync();
    await db.Entry(transacao).Reference(t => t.Categoria).LoadAsync();
    await db.Entry(transacao).Reference(t => t.Cartao).LoadAsync();
    await db.Entry(transacao).Reference(t => t.Conta).LoadAsync();
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

// Importação de extrato: grava a lista revisada de uma vez (todas ou nenhuma), já como pagas/recebidas.
rotas.MapPost("/transacoes/importar", async (List<ItemImportacao> itens, int? contaId, ClaimsPrincipal user, AppDbContext db) =>
{
    if (itens.Count == 0)
        return Results.BadRequest("Nenhuma transação para importar.");
    if (itens.Count > 2000)
        return Results.BadRequest("Importe no máximo 2.000 transações por vez.");

    var usuarioId = UsuarioId(user);
    if (contaId is not null && !await ContaDoUsuario(db, usuarioId, contaId.Value))
        return Results.BadRequest("Conta não encontrada.");
    contaId ??= await ContaPrincipal(db, usuarioId);

    var transacoes = new List<Transacao>();
    foreach (var item in itens)
    {
        var entrada = new Transacao
        {
            Descricao = item.Descricao ?? "",
            Valor = item.Valor,
            Data = item.Data,
            Tipo = item.Tipo,
            CategoriaId = item.CategoriaId,
            Pago = true, // veio do extrato: já aconteceu
        };
        var erro = await ValidarTransacao(entrada, usuarioId, db);
        if (erro is not null)
            return Results.BadRequest($"Linha {item.Linha}: {erro}");

        var transacao = new Transacao { UsuarioId = usuarioId, ContaId = contaId };
        CopiarCampos(entrada, transacao);
        transacoes.Add(transacao);
    }

    // Um único SaveChanges: grava todas ou nenhuma.
    db.Transacoes.AddRange(transacoes);
    await db.SaveChangesAsync();
    return Results.Ok(new { importadas = transacoes.Count });
});

// Marca só a situação (paga / pendente), sem mexer no resto da transação.
rotas.MapPut("/transacoes/{id}/pago", async (int id, Situacao entrada, ClaimsPrincipal user, AppDbContext db) =>
{
    var transacao = await DoUsuario(db.Transacoes.Include(t => t.Categoria).Include(t => t.Cartao).Include(t => t.Conta), user).FirstOrDefaultAsync(t => t.Id == id);
    if (transacao is null)
        return Results.NotFound();

    transacao.Pago = entrada.Pago;
    await db.SaveChangesAsync();
    return Results.Ok(transacao);
});

// Contas pendentes (a pagar e a receber) até o fim do mês, inclusive as atrasadas de meses anteriores.
// Sem mês: todas as pendentes.
rotas.MapGet("/pendentes", async (string? mes, ClaimsPrincipal user, AppDbContext db) =>
{
    var consulta = DoUsuario(db.Transacoes.Include(t => t.Categoria).Include(t => t.Cartao).Include(t => t.Conta), user).Where(t => !t.Pago);
    if (mes is not null)
    {
        if (!DateTime.TryParseExact(mes, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var inicio))
            return Results.BadRequest(MesInvalido);
        var fimDoMes = inicio.AddMonths(1);
        consulta = consulta.Where(t => t.Data < fimDoMes);
    }

    var pendentes = await consulta.OrderBy(t => t.Data).ThenBy(t => t.Id).ToListAsync();
    var hoje = DateTime.Today;
    return Results.Ok(new
    {
        aPagar = pendentes.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => t.Valor),
        aReceber = pendentes.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor),
        transacoes = pendentes.Select(t => new
        {
            t.Id,
            t.Descricao,
            t.Valor,
            t.Data,
            t.Tipo,
            categoria = t.Categoria?.Nome,
            t.SerieId,
            // Compras no cartão: o front junta as do mesmo cartão e vencimento numa linha só (a fatura).
            t.CartaoId,
            cartao = t.Cartao?.Nome,
            // Atrasada: a data já passou e ainda não foi paga/recebida.
            atrasada = t.Data < hoje,
        }),
    });
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

    // Saldo real: o dinheiro que se tem agora = tudo o que já foi recebido − tudo o que já foi pago
    // (de todas as datas; não depende do mês escolhido).
    // Começa com o saldo inicial das contas.
    var saldosIniciais = await SaldosIniciais(db, user);
    var saldoReal = saldosIniciais + await DoUsuario(db.Transacoes, user)
        .Where(t => t.Pago)
        .SumAsync(t => t.Tipo == TipoTransacao.Receita ? t.Valor : -t.Valor);

    if (mes is null)
        return Results.Ok(new { receitas, despesas, saldo = receitas - despesas, saldoReal });

    // Com mês: também o acumulado, ou seja, o saldo de tudo até o fim desse mês (previsão).
    var fimDoMes = DateTime.ParseExact(mes, "yyyy-MM", null).AddMonths(1);
    var acumulado = saldosIniciais + await SaldoAntesDe(DoUsuario(db.Transacoes, user), fimDoMes);
    return Results.Ok(new { receitas, despesas, saldo = receitas - despesas, acumulado, saldoReal });
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
    var acumulado = await SaldosIniciais(db, user) + await SaldoAntesDe(DoUsuario(db.Transacoes, user), inicio);

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

// Comparação do ano com o anterior: totais e despesas por categoria dos dois anos.
// No ano atual, compara só até o mês atual (jan–nov de 2026 com jan–nov de 2025),
// para a comparação ser justa; nos outros anos, os 12 meses.
rotas.MapGet("/resumo-anual", async (string? ano, ClaimsPrincipal user, AppDbContext db) =>
{
    var anoEscolhido = DateTime.Today.Year;
    if (ano is not null && (!int.TryParse(ano, out anoEscolhido) || anoEscolhido < 1900 || anoEscolhido > 2100))
        return Results.BadRequest("Ano inválido. Use quatro dígitos, por exemplo 2026.");

    var ateMes = anoEscolhido == DateTime.Today.Year ? DateTime.Today.Month : 12;
    var inicio = new DateTime(anoEscolhido, 1, 1);
    var fim = inicio.AddMonths(ateMes);
    var inicioAnterior = inicio.AddYears(-1);
    var fimAnterior = fim.AddYears(-1);

    // O banco devolve um total por (ano, tipo, categoria).
    var totais = await DoUsuario(db.Transacoes, user)
        .Where(t => (t.Data >= inicio && t.Data < fim) || (t.Data >= inicioAnterior && t.Data < fimAnterior))
        .GroupBy(t => new { t.Data.Year, t.Tipo, t.CategoriaId, t.Categoria!.Nome })
        .Select(g => new { Ano = g.Key.Year, g.Key.Tipo, g.Key.CategoriaId, g.Key.Nome, Total = g.Sum(t => t.Valor) })
        .ToListAsync();

    object TotaisDo(int a)
    {
        var receitas = totais.Where(x => x.Ano == a && x.Tipo == TipoTransacao.Receita).Sum(x => x.Total);
        var despesas = totais.Where(x => x.Ano == a && x.Tipo == TipoTransacao.Despesa).Sum(x => x.Total);
        return new { receitas, despesas, saldo = receitas - despesas };
    }

    // As categorias que mais mudaram (em reais) primeiro.
    var categorias = totais
        .Where(x => x.Tipo == TipoTransacao.Despesa)
        .GroupBy(x => new { x.CategoriaId, x.Nome })
        .Select(g => new
        {
            categoriaId = g.Key.CategoriaId,
            categoria = g.Key.Nome,
            atual = g.Where(x => x.Ano == anoEscolhido).Sum(x => x.Total),
            anterior = g.Where(x => x.Ano == anoEscolhido - 1).Sum(x => x.Total),
        })
        .OrderByDescending(c => Math.Abs(c.atual - c.anterior))
        .ThenByDescending(c => c.atual)
        .ToList();

    return Results.Ok(new
    {
        ano = anoEscolhido,
        ateMes,
        atual = TotaisDo(anoEscolhido),
        anterior = TotaisDo(anoEscolhido - 1),
        categorias,
    });
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
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(ClaimTypes.Email, usuario.Email),
    };
    if (usuario.VisitanteAte is not null)
        claims.Add(new Claim("visitante_ate", usuario.VisitanteAte.Value.ToString("O", CultureInfo.InvariantCulture)));
    var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
}

// Apaga as contas de visitante que já passaram do prazo, com tudo o que era delas.
// Usuários de verdade (VisitanteAte vazio) nunca entram nesta lista.
static async Task ApagarVisitantesVencidos(AppDbContext db)
{
    var agora = DateTime.UtcNow;
    var ids = await db.Usuarios.Where(u => u.VisitanteAte != null && u.VisitanteAte < agora)
        .Select(u => (int?)u.Id).ToListAsync();
    if (ids.Count == 0)
        return;

    // Na ordem certa: primeiro o que aponta para as contas, cartões e categorias.
    await db.Transferencias.Where(t => ids.Contains(t.UsuarioId)).ExecuteDeleteAsync();
    await db.Transacoes.Where(t => ids.Contains(t.UsuarioId)).ExecuteDeleteAsync();
    await db.Cartoes.Where(c => ids.Contains(c.UsuarioId)).ExecuteDeleteAsync();
    await db.Contas.Where(c => ids.Contains(c.UsuarioId)).ExecuteDeleteAsync();
    await db.Categorias.Where(c => ids.Contains(c.UsuarioId)).ExecuteDeleteAsync();
    await db.Usuarios.Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync();
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
    if (entrada.Valor > 99_999_999.99m) // o maior valor que a coluna decimal(10,2) guarda
        return "O valor é grande demais.";
    if (entrada.Data == default)
        return "A data é obrigatória.";
    if (!Enum.IsDefined(entrada.Tipo))
        return "O tipo deve ser Receita ou Despesa.";
    if (!await db.Categorias.AnyAsync(c => c.Id == entrada.CategoriaId && c.UsuarioId == usuarioId))
        return "Categoria não encontrada.";
    if (entrada.ContaId is not null && !await ContaDoUsuario(db, usuarioId, entrada.ContaId.Value))
        return "Conta não encontrada.";
    if (entrada.CartaoId is not null)
    {
        if (entrada.Tipo != TipoTransacao.Despesa)
            return "Só despesas podem ser feitas no cartão de crédito.";
        if (!await db.Cartoes.AnyAsync(c => c.Id == entrada.CartaoId && c.UsuarioId == usuarioId))
            return "Cartão não encontrado.";
    }
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
    destino.Pago = origem.Pago;
    destino.CartaoId = origem.CartaoId;
}

static Task<bool> ContaDoUsuario(AppDbContext db, int usuarioId, int contaId) =>
    db.Contas.AnyAsync(c => c.Id == contaId && c.UsuarioId == usuarioId);

// A conta principal = a mais antiga do usuário (a criada no cadastro, se ainda existir).
static Task<int?> ContaPrincipal(AppDbContext db, int usuarioId) =>
    db.Contas.Where(c => c.UsuarioId == usuarioId).OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync();

static async Task<decimal> SaldosIniciais(AppDbContext db, ClaimsPrincipal user)
{
    var usuarioId = UsuarioId(user);
    return await db.Contas.Where(c => c.UsuarioId == usuarioId).SumAsync(c => c.SaldoInicial);
}

static string? ValidarConta(Conta entrada)
{
    if (string.IsNullOrWhiteSpace(entrada.Nome))
        return "O nome da conta é obrigatório.";
    if (Math.Abs(entrada.SaldoInicial) > 99_999_999.99m)
        return "O saldo inicial é grande demais.";
    return null;
}

static string? ValidarCartao(Cartao entrada)
{
    if (string.IsNullOrWhiteSpace(entrada.Nome))
        return "O nome do cartão é obrigatório.";
    if (entrada.DiaFechamento is < 1 or > 28 || entrada.DiaVencimento is < 1 or > 28)
        return "Os dias de fechamento e de vencimento devem ser entre 1 e 28.";
    if (entrada.DiaFechamento == entrada.DiaVencimento)
        return "O vencimento deve ser num dia diferente do fechamento.";
    return null;
}

// Em que dia vence a fatura de uma compra feita nesse dia (a regra está no Cartao).
static DateTime VencimentoDaFatura(Cartao cartao, DateTime compra) => cartao.VencimentoDaFatura(compra);

record Credenciais(string? Email, string? Senha);
record TrocaDeSenha(string? SenhaAtual, string? NovaSenha);
record Situacao(bool Pago);
record MetaEconomia(decimal? MetaMensal);
// Uma linha do extrato importado ("Linha" = número da linha no arquivo, para as mensagens de erro).
record ItemImportacao(int Linha, string? Descricao, decimal Valor, DateTime Data, TipoTransacao Tipo, int CategoriaId);

// Deixa os testes automatizados (pasta tests) ligarem esta API em memória.
public partial class Program { }
