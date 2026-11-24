using ControleFinanceiro.Tests.Infra;

namespace ControleFinanceiro.Tests;

[Collection("api")]
public class CategoriasTestes(ApiDeTeste api)
{
    [Fact]
    public async Task Cria_edita_e_remove_a_meta()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var id = await usuario.CriarCategoria("Mercado", 800);

        var editada = await usuario.Put($"/categorias/{id}", new { nome = "  Supermercado ", metaMensal = 800.55m });
        Assert.Equal("Supermercado", editada.Dados!["nome"]!.GetValue<string>());
        Assert.Equal(800.55m, editada.Decimal("metaMensal"));

        var semMeta = await usuario.Put($"/categorias/{id}", new { nome = "Supermercado", metaMensal = (decimal?)null });
        Assert.Null(semMeta.Dados!["metaMensal"]);
    }

    [Theory]
    [InlineData("", null, "O nome da categoria é obrigatório.")]
    [InlineData("X", 0, "A meta mensal deve ser maior que zero (ou vazia, para não ter meta).")]
    [InlineData("X", -50, "A meta mensal deve ser maior que zero (ou vazia, para não ter meta).")]
    public async Task Categoria_invalida_da_400(string nome, int? meta, string mensagem)
    {
        var usuario = await Cliente.NovoUsuario(api);
        var r = await usuario.Post("/categorias", new { nome, metaMensal = meta });
        Assert.Equal(400, r.Status);
        Assert.Equal(mensagem, r.Mensagem);
        Assert.Empty((await usuario.Get("/categorias")).Dados!.AsArray());
    }

    [Fact]
    public async Task Nao_exclui_categoria_com_transacoes()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var usada = await usuario.CriarCategoria("Usada");
        var vazia = await usuario.CriarCategoria("Vazia");
        await usuario.CriarTransacao(usada, "Compra", 10, "2026-11-01");

        Assert.Equal(409, (await usuario.Delete($"/categorias/{usada}")).Status);
        Assert.Equal(204, (await usuario.Delete($"/categorias/{vazia}")).Status);
    }

    [Fact]
    public async Task Um_usuario_nao_ve_nem_mexe_nas_categorias_do_outro()
    {
        var ana = await Cliente.NovoUsuario(api);
        var bruno = await Cliente.NovoUsuario(api);
        var daAna = await ana.CriarCategoria("Da Ana");

        Assert.Empty((await bruno.Get("/categorias")).Dados!.AsArray());
        Assert.Equal(404, (await bruno.Get($"/categorias/{daAna}")).Status);
        Assert.Equal(404, (await bruno.Put($"/categorias/{daAna}", new { nome = "hack" })).Status);
        Assert.Equal(404, (await bruno.Delete($"/categorias/{daAna}")).Status);
        // Nem usar a categoria dela numa transação
        var r = await bruno.Post("/transacoes", new { descricao = "x", valor = 10, data = "2026-11-01", tipo = "Despesa", categoriaId = daAna });
        Assert.Equal("Categoria não encontrada.", r.Mensagem);
    }
}
