using ControleFinanceiro.Tests.Infra;

namespace ControleFinanceiro.Tests;

// Meta de economia mensal: salvar, ler, tirar e validar.
[Collection("api")]
public class MetaEconomiaTestes(ApiDeTeste api)
{
    private static decimal? Meta(Resposta r) => r.Dados!["metaMensal"]?.GetValue<decimal>();

    [Fact]
    public async Task Salvar_ler_e_tirar_a_meta()
    {
        var usuario = await Cliente.NovoUsuario(api);
        Assert.Null(Meta(await usuario.Get("/meta-economia"))); // começa sem meta

        var r = await usuario.Put("/meta-economia", new { metaMensal = 1000.50m });
        Assert.Equal(200, r.Status);
        Assert.Equal(1000.50m, Meta(r));
        Assert.Equal(1000.50m, Meta(await usuario.Get("/meta-economia")));

        Assert.Equal(200, (await usuario.Put("/meta-economia", new { metaMensal = (decimal?)null })).Status);
        Assert.Null(Meta(await usuario.Get("/meta-economia")));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-50")]
    [InlineData("100000000")]
    public async Task Valor_invalido_da_400(string valor)
    {
        var usuario = await Cliente.NovoUsuario(api);
        var r = await usuario.Put("/meta-economia", new { metaMensal = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture) });
        Assert.Equal(400, r.Status);
        Assert.Null(Meta(await usuario.Get("/meta-economia"))); // nada foi salvo
    }

    [Fact]
    public async Task Cada_usuario_tem_a_sua_meta_e_sem_login_da_401()
    {
        var ana = await Cliente.NovoUsuario(api);
        var bruno = await Cliente.NovoUsuario(api);
        await ana.Put("/meta-economia", new { metaMensal = 500 });

        Assert.Null(Meta(await bruno.Get("/meta-economia")));
        Assert.Equal(401, (await Cliente.SemLogin(api).Get("/meta-economia")).Status);
        Assert.Equal(401, (await Cliente.SemLogin(api).Put("/meta-economia", new { metaMensal = 1 })).Status);
    }
}
