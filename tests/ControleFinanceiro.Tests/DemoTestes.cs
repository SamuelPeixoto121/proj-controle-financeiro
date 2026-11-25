using ControleFinanceiro.Data;
using ControleFinanceiro.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Tests;

// O site de demonstração: cadastro fechado e "Entrar como visitante" com dados de exemplo.
[Collection("api")]
public class DemoTestes(ApiDeTeste api)
{
    private static async Task<Cliente> NovoVisitante(ApiDemo demo)
    {
        var visitante = Cliente.SemLogin(demo);
        var r = await visitante.Post("/auth/visitante");
        Assert.Equal(201, r.Status);
        Assert.True(r.Dados!["visitante"]!.GetValue<bool>());
        return visitante;
    }

    [Fact]
    public async Task No_PC_o_modo_demo_fica_desligado()
    {
        var cliente = Cliente.SemLogin(api);
        Assert.False((await cliente.Get("/auth/modo")).Dados!["demo"]!.GetValue<bool>());
        Assert.Equal(404, (await cliente.Post("/auth/visitante")).Status);

        var usuario = await Cliente.NovoUsuario(api);
        Assert.False((await usuario.Get("/auth/eu")).Dados!["visitante"]!.GetValue<bool>());
    }

    [Fact]
    public async Task No_demo_o_cadastro_fica_fechado()
    {
        await using var demo = new ApiDemo();
        var cliente = Cliente.SemLogin(demo);
        Assert.True((await cliente.Get("/auth/modo")).Dados!["demo"]!.GetValue<bool>());

        var r = await cliente.Post("/auth/cadastro", new { email = $"{Guid.NewGuid():N}@teste.com", senha = "senha123" });
        Assert.Equal(403, r.Status);
    }

    [Fact]
    public async Task Visitante_entra_com_dados_de_exemplo_so_dele()
    {
        await using var demo = new ApiDemo();
        var visitante = await NovoVisitante(demo);
        Assert.True((await visitante.Get("/auth/eu")).Dados!["visitante"]!.GetValue<bool>());

        var contas = (await visitante.Get("/contas")).Dados!.AsArray();
        Assert.Equal(["Conta corrente", "Poupança"], contas.Select(c => c!["nome"]!.GetValue<string>()));
        Assert.Equal(9, (await visitante.Get("/categorias")).Dados!.AsArray().Count);
        Assert.Single((await visitante.Get("/cartoes")).Dados!.AsArray());
        Assert.Equal(800m, (await visitante.Get("/meta-economia")).Decimal("metaMensal"));
        Assert.NotEmpty((await visitante.Get("/transferencias")).Dados!.AsArray());

        var transacoes = await visitante.Transacoes();
        Assert.True(transacoes.Count > 200);
        Assert.Contains(transacoes, t => t["cartaoId"] is not null);
        Assert.Contains(transacoes, t => !t["pago"]!.GetValue<bool>()); // o mês que vem ainda está pendente

        // Tem o ano passado inteiro: a comparação anual tem com o que comparar.
        var anual = await visitante.Get($"/resumo-anual?ano={DateTime.Today.Year}");
        Assert.True(anual.Dados!["anterior"]!["receitas"]!.GetValue<decimal>() > 0);

        // Outro visitante ganha a própria conta, com a mesma quantidade de dados (nada misturado).
        var outro = await NovoVisitante(demo);
        Assert.Equal(transacoes.Count, (await outro.Transacoes()).Count);
    }

    [Fact]
    public async Task Visitantes_vencidos_sao_apagados_e_usuarios_de_verdade_nao()
    {
        var deVerdade = await Cliente.NovoUsuario(api);
        await deVerdade.CriarTransacao(await deVerdade.CriarCategoria(), "Minha conta", 10, "2026-01-01");

        await using var demo = new ApiDemo();
        var vencido = await NovoVisitante(demo);
        var vencidoId = (await vencido.Get("/auth/eu")).Dados!["id"]!.GetValue<int>();
        var recente = await NovoVisitante(demo);
        var recenteId = (await recente.Get("/auth/eu")).Dados!["id"]!.GetValue<int>();

        using var escopo = demo.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Usuarios.Where(u => u.Id == vencidoId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.VisitanteAte, DateTime.UtcNow.AddMinutes(-1)));

        await NovoVisitante(demo); // a limpeza acontece quando entra um visitante novo

        Assert.False(await db.Usuarios.AnyAsync(u => u.Id == vencidoId));
        Assert.False(await db.Transacoes.AnyAsync(t => t.UsuarioId == vencidoId));
        Assert.False(await db.Contas.AnyAsync(c => c.UsuarioId == vencidoId));
        Assert.True(await db.Usuarios.AnyAsync(u => u.Id == recenteId));
        Assert.Single(await deVerdade.Transacoes("Minha conta"));
    }
}
