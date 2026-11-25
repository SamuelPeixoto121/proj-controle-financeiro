using System.Text.RegularExpressions;
using ControleFinanceiro.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Tests.Infra;

// Liga a API de verdade em memória (sem abrir porta), apontando para o banco de TESTES.
// O banco é recriado do zero no começo e apagado no fim. O banco de verdade nunca é usado.
public class ApiDeTeste : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BancoDeTestes = "controle_financeiro_testes";

    // Os testes fazem dezenas de cadastros por minuto; o limite real (5) tem um teste só dele.
    protected virtual int LimiteLogin => 1000;

    // Liga o modo do site de demonstração (cadastro fechado, entrar como visitante).
    protected virtual bool Demo => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // A mesma conexão dos User Secrets da API, trocando só o nome do banco.
        var conexao = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build()
            .GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Conexão não configurada. Rode na pasta backend: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\" (veja o README).");
        var conexaoDeTeste = Regex.Replace(conexao, "Database=[^;]*", $"Database={BancoDeTestes}", RegexOptions.IgnoreCase);
        if (!conexaoDeTeste.Contains($"Database={BancoDeTestes}"))
            throw new InvalidOperationException("A conexão não tem \"Database=\"; não dá para apontar para o banco de testes.");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = conexaoDeTeste,
            ["LimiteLogin:PorMinuto"] = LimiteLogin.ToString(),
            ["Demo:Ativo"] = Demo.ToString(),
        }));
    }

    // Antes de todos os testes: confere o nome do banco e recria as tabelas do zero.
    public async Task InitializeAsync()
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        GarantirBancoDeTestes(db);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    // Depois de todos os testes: apaga o banco de testes.
    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var escopo = Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            GarantirBancoDeTestes(db);
            await db.Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }

    // Trava de segurança: se, por qualquer motivo, o banco não for o de testes, nada é apagado.
    private static void GarantirBancoDeTestes(AppDbContext db)
    {
        var nome = db.Database.GetDbConnection().Database;
        if (nome != BancoDeTestes)
            throw new InvalidOperationException($"Recusado: os testes só rodam no banco '{BancoDeTestes}', e a conexão aponta para '{nome}'.");
    }
}

// A mesma API, mas com o limite de login de verdade (5 por minuto). Usa o banco já criado pela ApiDeTeste.
public class ApiComLimiteReal : ApiDeTeste
{
    protected override int LimiteLogin => 5;
}

// A mesma API no modo do site de demonstração. Usa o banco já criado pela ApiDeTeste.
public class ApiDemo : ApiDeTeste
{
    protected override bool Demo => true;
}
