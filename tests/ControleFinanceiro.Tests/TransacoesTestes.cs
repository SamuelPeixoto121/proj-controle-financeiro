using ControleFinanceiro.Tests.Infra;

namespace ControleFinanceiro.Tests;

[Collection("api")]
public class TransacoesTestes(ApiDeTeste api)
{
    private static string Data(System.Text.Json.Nodes.JsonNode t) => t["data"]!.GetValue<string>()[..10];

    [Fact]
    public async Task Cria_edita_e_exclui()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var criada = await usuario.CriarTransacao(cat, "  Mercado ", 123.45m, "2026-11-05");
        Assert.Equal("Mercado", criada["descricao"]!.GetValue<string>());
        Assert.Equal("Geral", criada["categoria"]!["nome"]!.GetValue<string>());
        var id = criada["id"]!.GetValue<int>();

        var editada = await usuario.Put($"/transacoes/{id}", new { descricao = "Feira", valor = 50, data = "2026-11-06", tipo = "Receita", categoriaId = cat, pago = false });
        Assert.Equal(200, editada.Status);
        Assert.Equal("Receita", editada.Dados!["tipo"]!.GetValue<string>());
        Assert.False(editada.Dados["pago"]!.GetValue<bool>());

        Assert.Equal(204, (await usuario.Delete($"/transacoes/{id}")).Status);
        Assert.Equal(404, (await usuario.Get($"/transacoes/{id}")).Status);
    }

    [Theory]
    [InlineData("", 10, "A descrição é obrigatória.")]
    [InlineData("x", 0, "O valor deve ser maior que zero.")]
    [InlineData("x", -5, "O valor deve ser maior que zero.")]
    [InlineData("x", 100000000, "O valor é grande demais.")]
    public async Task Transacao_invalida_da_400_e_nao_grava(string descricao, decimal valor, string mensagem)
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var r = await usuario.Post("/transacoes", new { descricao, valor, data = "2026-11-01", tipo = "Despesa", categoriaId = cat });
        Assert.Equal(400, r.Status);
        Assert.Equal(mensagem, r.Mensagem);
        Assert.Empty(await usuario.Transacoes());
    }

    [Fact]
    public async Task Repetir_cria_uma_por_mes_e_o_dia_31_cai_no_fim_dos_meses_curtos()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Aluguel", 1200, "2027-01-31", consulta: "?repetir=4");

        var lista = await usuario.Transacoes("Aluguel");
        Assert.Equal(["2027-01-31", "2027-02-28", "2027-03-31", "2027-04-30"], lista.Select(Data));
        var serie = lista[0]["serieId"]!.GetValue<string>();
        Assert.All(lista, t => Assert.Equal(serie, t["serieId"]!.GetValue<string>()));
        // Só a 1ª segue o "pago"; as seguintes começam pendentes.
        Assert.Equal([true, false, false, false], lista.Select(t => t["pago"]!.GetValue<bool>()));
    }

    // Valores em texto: número com vírgula no [InlineData] vira double, que não guarda centavos exatos.
    [Theory]
    [InlineData("100", 3, "33.34", "33.33")]
    [InlineData("1200", 10, "120", "120")]
    [InlineData("1234.56", 7, "176.40", "176.36")]
    [InlineData("0.10", 3, "0.04", "0.03")]
    public async Task Parcelas_dividem_o_valor_e_a_soma_bate(string totalTexto, int parcelas, string primeiraTexto, string demaisTexto)
    {
        var total = decimal.Parse(totalTexto, System.Globalization.CultureInfo.InvariantCulture);
        var primeira = decimal.Parse(primeiraTexto, System.Globalization.CultureInfo.InvariantCulture);
        var demais = decimal.Parse(demaisTexto, System.Globalization.CultureInfo.InvariantCulture);
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Compra", total, "2026-11-15", consulta: $"?parcelas={parcelas}");

        var lista = await usuario.Transacoes("Compra");
        Assert.Equal(parcelas, lista.Count);
        Assert.Equal(primeira, lista[0]["valor"]!.GetValue<decimal>());
        Assert.All(lista.Skip(1), t => Assert.Equal(demais, t["valor"]!.GetValue<decimal>()));
        Assert.Equal(total, lista.Sum(t => t["valor"]!.GetValue<decimal>()));
        Assert.Equal($"Compra (1/{parcelas})", lista[0]["descricao"]!.GetValue<string>());
        Assert.Equal($"Compra ({parcelas}/{parcelas})", lista[^1]["descricao"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("?parcelas=1", "O número de parcelas deve ser entre 2 e 60.")]
    [InlineData("?parcelas=61", "O número de parcelas deve ser entre 2 e 60.")]
    [InlineData("?parcelas=abc", "O número de parcelas deve ser entre 2 e 60.")]
    [InlineData("?repetir=0", "Repetir deve ser um número de meses entre 1 e 60.")]
    [InlineData("?repetir=3&parcelas=3", "Escolha repetir ou parcelar, não os dois.")]
    public async Task Repetir_ou_parcelar_invalido_da_400_e_nao_grava(string consulta, string mensagem)
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var r = await usuario.Post($"/transacoes{consulta}", new { descricao = "x", valor = 100, data = "2026-11-01", tipo = "Despesa", categoriaId = cat });
        Assert.Equal(400, r.Status);
        Assert.Equal(mensagem, r.Mensagem);
        Assert.Empty(await usuario.Transacoes());
    }

    [Fact]
    public async Task Parcela_menor_que_um_centavo_da_400()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var r = await usuario.Post("/transacoes?parcelas=10", new { descricao = "x", valor = 0.05, data = "2026-11-01", tipo = "Despesa", categoriaId = cat });
        Assert.Equal(400, r.Status);
        Assert.Empty(await usuario.Transacoes());
    }

    [Fact]
    public async Task Editar_com_seguintes_muda_daquele_mes_em_diante_e_mantem_a_numeracao()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Geladeira", 1000, "2026-11-10", consulta: "?parcelas=5");
        var lista = await usuario.Transacoes("Geladeira");

        var r = await usuario.Put($"/transacoes/{lista[2]["id"]}?seguintes=true",
            new { descricao = "Geladeira Nova (3/5)", valor = 250, data = Data(lista[2]), tipo = "Despesa", categoriaId = cat, pago = true });
        Assert.Equal(200, r.Status);

        lista = await usuario.Transacoes("Geladeira");
        Assert.Equal(["Geladeira (1/5)", "Geladeira (2/5)", "Geladeira Nova (3/5)", "Geladeira Nova (4/5)", "Geladeira Nova (5/5)"],
            lista.Select(t => t["descricao"]!.GetValue<string>()));
        Assert.Equal([200m, 200m, 250m, 250m, 250m], lista.Select(t => t["valor"]!.GetValue<decimal>()));
        // A situação (pago) não passa para as seguintes: só a editada ficou paga.
        Assert.Equal([true, false, true, false, false], lista.Select(t => t["pago"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Excluir_com_seguintes_apaga_daquele_mes_em_diante_so_daquela_serie()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Luz", 100, "2026-01-10", consulta: "?repetir=6");
        await usuario.CriarTransacao(cat, "Água", 50, "2026-01-15", consulta: "?repetir=6");
        var luz = await usuario.Transacoes("Luz");

        Assert.Equal(204, (await usuario.Delete($"/transacoes/{luz[3]["id"]}?seguintes=true")).Status);

        Assert.Equal(3, (await usuario.Transacoes("Luz")).Count);
        Assert.Equal(6, (await usuario.Transacoes("Água")).Count);
    }

    [Fact]
    public async Task Marcar_como_paga_muda_so_a_situacao()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var t = await usuario.CriarTransacao(cat, "Conta", 80, "2026-11-20", pago: false);

        var r = await usuario.Put($"/transacoes/{t["id"]}/pago", new { pago = true });
        Assert.True(r.Dados!["pago"]!.GetValue<bool>());
        Assert.Equal(80m, r.Decimal("valor"));
        Assert.Equal("Conta", r.Dados["descricao"]!.GetValue<string>());

        var outro = await Cliente.NovoUsuario(api);
        Assert.Equal(404, (await outro.Put($"/transacoes/{t["id"]}/pago", new { pago = false })).Status);
    }

    [Fact]
    public async Task Filtro_por_mes_e_mes_invalido()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Outubro", 1, "2026-10-31");
        await usuario.CriarTransacao(cat, "Novembro 1", 1, "2026-11-01");
        await usuario.CriarTransacao(cat, "Novembro 30", 1, "2026-11-30");
        await usuario.CriarTransacao(cat, "Dezembro", 1, "2026-12-01");

        var novembro = await usuario.Get("/transacoes?mes=2026-11");
        Assert.Equal(2, novembro.Dados!.AsArray().Count);
        Assert.Equal(400, (await usuario.Get("/transacoes?mes=2026-13")).Status);
    }

    [Fact]
    public async Task Importar_grava_todas_ou_nenhuma_e_diz_a_linha_do_erro()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        object Item(int linha, string descricao, decimal valor) =>
            new { linha, descricao, valor, data = "2026-11-05", tipo = "Despesa", categoriaId = cat };

        var ruim = await usuario.Post("/transacoes/importar", new[] { Item(2, "ok", 10), Item(7, "ruim", -5) });
        Assert.Equal(400, ruim.Status);
        Assert.Equal("Linha 7: O valor deve ser maior que zero.", ruim.Mensagem);
        Assert.Empty(await usuario.Transacoes());

        var boa = await usuario.Post("/transacoes/importar", new[] { Item(2, "A", 10), Item(3, "B", 20.5m) });
        Assert.Equal(2, boa.Dados!["importadas"]!.GetValue<int>());
        var lista = await usuario.Transacoes();
        Assert.Equal(2, lista.Count);
        Assert.All(lista, t => Assert.True(t["pago"]!.GetValue<bool>())); // do extrato = já pagas

        Assert.Equal("Nenhuma transação para importar.", (await usuario.Post("/transacoes/importar", Array.Empty<object>())).Mensagem);
        var demais = Enumerable.Range(0, 2001).Select(i => Item(i + 2, "x", 1)).ToArray();
        Assert.Equal(400, (await usuario.Post("/transacoes/importar", demais)).Status);
    }

    [Fact]
    public async Task Um_usuario_nao_ve_nem_mexe_nas_transacoes_do_outro()
    {
        var ana = await Cliente.NovoUsuario(api);
        var bruno = await Cliente.NovoUsuario(api);
        var cat = await ana.CriarCategoria();
        var t = await ana.CriarTransacao(cat, "Da Ana", 10, "2026-11-01", consulta: "?repetir=3");
        var id = t["id"]!.GetValue<int>();

        Assert.Empty(await bruno.Transacoes());
        Assert.Equal(404, (await bruno.Get($"/transacoes/{id}")).Status);
        Assert.Equal(404, (await bruno.Delete($"/transacoes/{id}?seguintes=true")).Status);
        Assert.Equal(3, (await ana.Transacoes()).Count);
    }
}
