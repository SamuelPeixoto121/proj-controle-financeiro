using System.Text.Json.Nodes;
using ControleFinanceiro.Tests.Infra;

namespace ControleFinanceiro.Tests;

// Cartão de crédito: cadastro, em qual fatura cada compra entra, parcelas, faturas e pagamento.
[Collection("api")]
public class CartoesTestes(ApiDeTeste api)
{
    private static async Task<int> CriarCartao(Cliente usuario, int fechamento, int vencimento, string nome = "Nubank")
    {
        var r = await usuario.Post("/cartoes", new { nome, diaFechamento = fechamento, diaVencimento = vencimento });
        Assert.Equal(201, r.Status);
        return r.Dados!["id"]!.GetValue<int>();
    }

    private static Task<Resposta> Comprar(Cliente usuario, int categoriaId, int? cartaoId, string data,
        decimal valor = 100, string descricao = "Compra", string tipo = "Despesa", string consulta = "") =>
        usuario.Post($"/transacoes{consulta}", new { descricao, valor, data, tipo, categoriaId, cartaoId, pago = false });

    private static string Dia(JsonNode? data) => data!.GetValue<string>()[..10];

    [Theory]
    // Fecha dia 3, vence dia 10 (vence no mesmo mês do fechamento)
    [InlineData(3, 10, "2030-12-01", "2030-12-10")] // antes do fechamento: fatura deste mês
    [InlineData(3, 10, "2030-12-03", "2031-01-10")] // no dia do fechamento: próxima fatura (e vira o ano)
    [InlineData(3, 10, "2030-12-31", "2031-01-10")]
    // Fecha dia 25, vence dia 5 (vence no mês seguinte ao do fechamento)
    [InlineData(25, 5, "2030-12-24", "2031-01-05")]
    [InlineData(25, 5, "2030-12-25", "2031-02-05")]
    [InlineData(25, 5, "2031-01-10", "2031-02-05")]
    public async Task Compra_entra_na_fatura_certa(int fechamento, int vencimento, string compra, string vence)
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var cartao = await CriarCartao(usuario, fechamento, vencimento);

        var r = await Comprar(usuario, cat, cartao, compra);
        Assert.Equal(201, r.Status);
        Assert.Equal(vence, Dia(r.Dados!["data"]));        // conta no mês do vencimento
        Assert.Equal(compra, Dia(r.Dados!["dataCompra"])); // e guarda o dia da compra
        Assert.Equal("Nubank", r.Dados!["cartao"]!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Parcelado_no_cartao_vai_uma_parcela_por_fatura_e_repetir_e_uma_compra_por_mes()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var cartao = await CriarCartao(usuario, 3, 10);

        Assert.Equal(201, (await Comprar(usuario, cat, cartao, "2030-12-05", 100, "Geladeira", consulta: "?parcelas=3")).Status);
        var parcelas = await usuario.Transacoes("Geladeira");
        Assert.Equal(["2031-01-10", "2031-02-10", "2031-03-10"], parcelas.Select(t => Dia(t["data"])));
        Assert.All(parcelas, t => Assert.Equal("2030-12-05", Dia(t["dataCompra"])));
        Assert.Equal([33.34m, 33.33m, 33.33m], parcelas.Select(t => t["valor"]!.GetValue<decimal>()));

        Assert.Equal(201, (await Comprar(usuario, cat, cartao, "2030-12-05", 39.90m, "Streaming", consulta: "?repetir=2")).Status);
        var assinatura = await usuario.Transacoes("Streaming");
        Assert.Equal(["2030-12-05", "2031-01-05"], assinatura.Select(t => Dia(t["dataCompra"])));
        Assert.Equal(["2031-01-10", "2031-02-10"], assinatura.Select(t => Dia(t["data"])));
    }

    [Fact]
    public async Task Editar_recalcula_a_fatura_e_a_parcela_continua_nas_faturas_seguintes()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var nubank = await CriarCartao(usuario, 3, 10);
        var inter = await CriarCartao(usuario, 20, 28, "Inter");
        await Comprar(usuario, cat, nubank, "2030-12-05", 300, "TV", consulta: "?parcelas=3");
        var segunda = (await usuario.Transacoes("TV"))[1]; // vence 10/02/2031
        var id = segunda["id"]!.GetValue<int>();

        // Comprou antes do fechamento (01/12): a 1ª fatura passa a ser 10/12 e a 2ª parcela, 10/01.
        var r = await usuario.Put($"/transacoes/{id}", new { descricao = "TV (2/3)", valor = 100, data = "2030-12-01", tipo = "Despesa", categoriaId = cat, cartaoId = nubank, pago = false });
        Assert.Equal(200, r.Status);
        Assert.Equal("2031-01-10", Dia(r.Dados!["data"]));

        // Outro cartão (fecha 20, vence 28): compra em 01/12 -> fatura de 28/12; 2ª parcela -> 28/01.
        r = await usuario.Put($"/transacoes/{id}", new { descricao = "TV (2/3)", valor = 100, data = "2030-12-01", tipo = "Despesa", categoriaId = cat, cartaoId = inter, pago = false });
        Assert.Equal("2031-01-28", Dia(r.Dados!["data"]));

        // Sem cartão: volta a ser uma transação comum, com a data informada.
        r = await usuario.Put($"/transacoes/{id}", new { descricao = "TV (2/3)", valor = 100, data = "2030-12-20", tipo = "Despesa", categoriaId = cat, pago = false });
        Assert.Equal("2030-12-20", Dia(r.Dados!["data"]));
        Assert.Null(r.Dados!["dataCompra"]);
        Assert.Null(r.Dados!["cartaoId"]);
    }

    [Fact]
    public async Task Faturas_agrupam_as_compras_e_pagar_marca_todas()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria("Mercado");
        var cartao = await CriarCartao(usuario, 3, 10);
        await Comprar(usuario, cat, cartao, "2020-11-05", 50.25m, "Padaria");   // vence 10/12
        await Comprar(usuario, cat, cartao, "2020-12-02", 200, "Mercado");      // vence 10/12
        await Comprar(usuario, cat, cartao, "2020-12-03", 80, "Farmácia");      // vence 10/01
        await usuario.CriarTransacao(cat, "Sem cartão", 999, "2020-12-10", pago: false); // não entra
        await Comprar(usuario, cat, cartao, "2099-06-01", 10, "Futura");       // vence 10/06/2099

        var faturas = (await usuario.Get($"/cartoes/{cartao}/faturas")).Dados!.AsArray();
        Assert.Equal(["2020-12-10", "2021-01-10", "2099-06-10"], faturas.Select(f => Dia(f!["vencimento"])));
        Assert.Equal("2020-12-03", Dia(faturas[0]!["fechamento"]));
        Assert.Equal("Aberta", faturas[2]!["situacao"]!.GetValue<string>()); // ainda não fechou
        Assert.Equal(250.25m, faturas[0]!["total"]!.GetValue<decimal>());
        Assert.Equal(["Padaria", "Mercado"], faturas[0]!["compras"]!.AsArray().Select(c => c!["descricao"]!.GetValue<string>()));
        Assert.Equal("Atrasada", faturas[0]!["situacao"]!.GetValue<string>()); // vencimento já passou e não foi paga

        // As contas pendentes trazem o cartão, para o front juntar a fatura numa linha.
        var pendentes = (await usuario.Get("/pendentes?mes=2020-12")).Dados!["transacoes"]!.AsArray();
        Assert.Equal(["Nubank", "Nubank", null], pendentes.Select(t => t!["cartao"]?.GetValue<string>()));

        // Pagar a fatura de 10/12: as duas compras ficam pagas; a de janeiro, não.
        var r = await usuario.Put($"/cartoes/{cartao}/faturas/2020-12-10/pago", new { pago = true });
        Assert.Equal(200, r.Status);
        Assert.Equal(2, r.Dados!["compras"]!.GetValue<int>());
        faturas = (await usuario.Get($"/cartoes/{cartao}/faturas")).Dados!.AsArray();
        Assert.Equal("Paga", faturas[0]!["situacao"]!.GetValue<string>());
        Assert.NotEqual("Paga", faturas[1]!["situacao"]!.GetValue<string>());

        // Desfazer
        await usuario.Put($"/cartoes/{cartao}/faturas/2020-12-10/pago", new { pago = false });
        faturas = (await usuario.Get($"/cartoes/{cartao}/faturas")).Dados!.AsArray();
        Assert.NotEqual("Paga", faturas[0]!["situacao"]!.GetValue<string>());

        Assert.Equal(404, (await usuario.Put($"/cartoes/{cartao}/faturas/2020-12-11/pago", new { pago = true })).Status);
        Assert.Equal(400, (await usuario.Put($"/cartoes/{cartao}/faturas/10-12-2020/pago", new { pago = true })).Status);
    }

    [Fact]
    public async Task Relatorios_contam_a_compra_no_mes_do_vencimento()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var cartao = await CriarCartao(usuario, 3, 10);
        await Comprar(usuario, cat, cartao, "2030-12-05", 120); // vence 10/01/2031

        Assert.Equal(0m, (await usuario.Get("/saldo?mes=2030-12")).Decimal("despesas"));
        Assert.Equal(120m, (await usuario.Get("/saldo?mes=2031-01")).Decimal("despesas"));
        Assert.Single((await usuario.Get("/transacoes?mes=2031-01")).Dados!.AsArray());
    }

    [Fact]
    public async Task Validacoes_e_exclusao_de_cartao()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var cartao = await CriarCartao(usuario, 3, 10);

        foreach (var (fechamento, vencimento) in new[] { (0, 10), (3, 29), (5, 5) })
            Assert.Equal(400, (await usuario.Post("/cartoes", new { nome = "X", diaFechamento = fechamento, diaVencimento = vencimento })).Status);
        Assert.Equal(400, (await usuario.Post("/cartoes", new { nome = " ", diaFechamento = 3, diaVencimento = 10 })).Status);

        var receita = await Comprar(usuario, cat, cartao, "2030-12-05", tipo: "Receita");
        Assert.Equal(400, receita.Status);
        Assert.Equal("Só despesas podem ser feitas no cartão de crédito.", receita.Mensagem);

        // Editar os dias
        var r = await usuario.Put($"/cartoes/{cartao}", new { nome = "Nubank Ultra", diaFechamento = 5, diaVencimento = 12 });
        Assert.Equal(200, r.Status);
        Assert.Equal(12, r.Dados!["diaVencimento"]!.GetValue<int>());

        // Com compras não pode excluir; sem compras, pode.
        await Comprar(usuario, cat, cartao, "2030-12-05");
        Assert.Equal(409, (await usuario.Delete($"/cartoes/{cartao}")).Status);
        var vazio = await CriarCartao(usuario, 1, 8, "Vazio");
        Assert.Equal(204, (await usuario.Delete($"/cartoes/{vazio}")).Status);
        Assert.Equal(["Nubank Ultra"], (await usuario.Get("/cartoes")).Dados!.AsArray().Select(c => c!["nome"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Cartao_de_outro_usuario_nao_aparece_nem_pode_ser_usado()
    {
        var ana = await Cliente.NovoUsuario(api);
        var bruno = await Cliente.NovoUsuario(api);
        var cartaoDaAna = await CriarCartao(ana, 3, 10);
        await Comprar(ana, await ana.CriarCategoria(), cartaoDaAna, "2030-12-05");
        var catDoBruno = await bruno.CriarCategoria();

        Assert.Empty((await bruno.Get("/cartoes")).Dados!.AsArray());
        Assert.Equal(404, (await bruno.Get($"/cartoes/{cartaoDaAna}/faturas")).Status);
        Assert.Equal(404, (await bruno.Put($"/cartoes/{cartaoDaAna}", new { nome = "x", diaFechamento = 1, diaVencimento = 8 })).Status);
        Assert.Equal(404, (await bruno.Delete($"/cartoes/{cartaoDaAna}")).Status);
        Assert.Equal(404, (await bruno.Put($"/cartoes/{cartaoDaAna}/faturas/2031-01-10/pago", new { pago = true })).Status);
        var r = await Comprar(bruno, catDoBruno, cartaoDaAna, "2030-12-05");
        Assert.Equal(400, r.Status);
        Assert.Equal("Cartão não encontrado.", r.Mensagem);
    }
}
