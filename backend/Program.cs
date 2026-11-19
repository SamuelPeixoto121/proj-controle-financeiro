using System.Text.Json.Serialization;
using ControleFinanceiro.Data;
using ControleFinanceiro.Models;
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
            .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

const string MesInvalido = "Mês inválido. Use o formato AAAA-MM, por exemplo 2026-11.";

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.MapGet("/", () => "API de controle financeiro no ar");

// Categorias
app.MapGet("/categorias", async (AppDbContext db) =>
    await db.Categorias.OrderBy(c => c.Nome).ToListAsync());

app.MapGet("/categorias/{id}", async (int id, AppDbContext db) =>
    await db.Categorias.FindAsync(id) is Categoria categoria
        ? Results.Ok(categoria)
        : Results.NotFound());

app.MapPost("/categorias", async (Categoria entrada, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(entrada.Nome))
        return Results.BadRequest("O nome da categoria é obrigatório.");

    var categoria = new Categoria { Nome = entrada.Nome.Trim() };
    db.Categorias.Add(categoria);
    await db.SaveChangesAsync();
    return Results.Created($"/categorias/{categoria.Id}", categoria);
});

app.MapPut("/categorias/{id}", async (int id, Categoria entrada, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(entrada.Nome))
        return Results.BadRequest("O nome da categoria é obrigatório.");

    var categoria = await db.Categorias.FindAsync(id);
    if (categoria is null)
        return Results.NotFound();

    categoria.Nome = entrada.Nome.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(categoria);
});

app.MapDelete("/categorias/{id}", async (int id, AppDbContext db) =>
{
    var categoria = await db.Categorias.FindAsync(id);
    if (categoria is null)
        return Results.NotFound();

    if (await db.Transacoes.AnyAsync(t => t.CategoriaId == id))
        return Results.Conflict("Não é possível excluir uma categoria que possui transações.");

    db.Categorias.Remove(categoria);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Transações
app.MapGet("/transacoes", async (string? mes, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(db.Transacoes.Include(t => t.Categoria), mes);
    if (consulta is null)
        return Results.BadRequest(MesInvalido);

    return Results.Ok(await consulta
        .OrderByDescending(t => t.Data)
        .ThenByDescending(t => t.Id)
        .ToListAsync());
});

app.MapGet("/transacoes/{id}", async (int id, AppDbContext db) =>
    await db.Transacoes.Include(t => t.Categoria).FirstOrDefaultAsync(t => t.Id == id) is Transacao transacao
        ? Results.Ok(transacao)
        : Results.NotFound());

app.MapPost("/transacoes", async (Transacao entrada, AppDbContext db) =>
{
    var erro = await ValidarTransacao(entrada, db);
    if (erro is not null)
        return Results.BadRequest(erro);

    var transacao = new Transacao();
    CopiarCampos(entrada, transacao);
    db.Transacoes.Add(transacao);
    await db.SaveChangesAsync();
    await db.Entry(transacao).Reference(t => t.Categoria).LoadAsync();
    return Results.Created($"/transacoes/{transacao.Id}", transacao);
});

app.MapPut("/transacoes/{id}", async (int id, Transacao entrada, AppDbContext db) =>
{
    var transacao = await db.Transacoes.FindAsync(id);
    if (transacao is null)
        return Results.NotFound();

    var erro = await ValidarTransacao(entrada, db);
    if (erro is not null)
        return Results.BadRequest(erro);

    CopiarCampos(entrada, transacao);
    await db.SaveChangesAsync();
    await db.Entry(transacao).Reference(t => t.Categoria).LoadAsync();
    return Results.Ok(transacao);
});

app.MapDelete("/transacoes/{id}", async (int id, AppDbContext db) =>
{
    var transacao = await db.Transacoes.FindAsync(id);
    if (transacao is null)
        return Results.NotFound();

    db.Transacoes.Remove(transacao);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Saldo
app.MapGet("/saldo", async (string? mes, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(db.Transacoes, mes);
    if (consulta is null)
        return Results.BadRequest(MesInvalido);

    var receitas = await consulta
        .Where(t => t.Tipo == TipoTransacao.Receita)
        .SumAsync(t => t.Valor);
    var despesas = await consulta
        .Where(t => t.Tipo == TipoTransacao.Despesa)
        .SumAsync(t => t.Valor);

    return Results.Ok(new { receitas, despesas, saldo = receitas - despesas });
});

// Gráfico: total de despesas por categoria, do maior para o menor
app.MapGet("/despesas-por-categoria", async (string? mes, AppDbContext db) =>
{
    var consulta = FiltrarPorMes(db.Transacoes, mes);
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

app.Run();

static async Task<string?> ValidarTransacao(Transacao entrada, AppDbContext db)
{
    if (string.IsNullOrWhiteSpace(entrada.Descricao))
        return "A descrição é obrigatória.";
    if (entrada.Valor <= 0)
        return "O valor deve ser maior que zero.";
    if (entrada.Data == default)
        return "A data é obrigatória.";
    if (!Enum.IsDefined(entrada.Tipo))
        return "O tipo deve ser Receita ou Despesa.";
    if (!await db.Categorias.AnyAsync(c => c.Id == entrada.CategoriaId))
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
