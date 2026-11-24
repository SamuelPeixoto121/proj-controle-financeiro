using ControleFinanceiro.Tests.Infra;

namespace ControleFinanceiro.Tests;

// Saldo, saldo real, acumulado, resumo mensal, despesas por categoria, metas e contas pendentes.
[Collection("api")]
public class RelatoriosTestes(ApiDeTeste api)
{
    [Fact]
    public async Task Saldo_do_mes_acumulado_e_saldo_real()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Salário out", 5000, "2030-10-05", "Receita");
        await usuario.CriarTransacao(cat, "Aluguel out", 1200.50m, "2030-10-10");
        await usuario.CriarTransacao(cat, "Mercado nov", 300, "2030-11-02");
        await usuario.CriarTransacao(cat, "Conta nov (pendente)", 800, "2030-11-20", pago: false);
        await usuario.CriarTransacao(cat, "Freela nov (a receber)", 2000, "2030-11-25", "Receita", pago: false);

        var tudo = await usuario.Get("/saldo");
        Assert.Equal(7000m, tudo.Decimal("receitas"));
        Assert.Equal(2300.50m, tudo.Decimal("despesas"));
        Assert.Equal(4699.50m, tudo.Decimal("saldo"));
        Assert.Null(tudo.Dados!["acumulado"]); // sem mês, não há acumulado
        Assert.Equal(3499.50m, tudo.Decimal("saldoReal")); // só o que já foi pago e recebido

        var novembro = await usuario.Get("/saldo?mes=2030-11");
        Assert.Equal(900m, novembro.Decimal("saldo"));
        Assert.Equal(4699.50m, novembro.Decimal("acumulado")); // outubro + novembro
        Assert.Equal(3499.50m, novembro.Decimal("saldoReal")); // não depende do mês

        Assert.Equal(3799.50m, (await usuario.Get("/saldo?mes=2030-10")).Decimal("acumulado"));
        Assert.Equal(0m, (await usuario.Get("/saldo?mes=2030-09")).Decimal("acumulado"));
    }

    [Fact]
    public async Task Resumo_mensal_tem_12_meses_e_o_acumulado_vem_dos_anos_anteriores()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Ano anterior", 1000, "2030-12-31", "Receita");
        await usuario.CriarTransacao(cat, "Janeiro", 200, "2031-01-01");
        await usuario.CriarTransacao(cat, "Março", 50.25m, "2031-03-31", "Receita");
        await usuario.CriarTransacao(cat, "Ano seguinte", 999, "2032-01-01");

        var meses = (await usuario.Get("/resumo-mensal?ano=2031")).Dados!.AsArray();
        Assert.Equal(12, meses.Count);
        Assert.Equal("2031-01", meses[0]!["mes"]!.GetValue<string>());
        decimal Acumulado(int i) => meses[i]!["acumulado"]!.GetValue<decimal>();
        Assert.Equal(800m, Acumulado(0));    // 1000 do ano anterior − 200
        Assert.Equal(800m, Acumulado(1));    // fevereiro vazio repete
        Assert.Equal(850.25m, Acumulado(2));
        Assert.Equal(850.25m, Acumulado(11)); // não conta 2032
        Assert.Equal(0m, meses[1]!["receitas"]!.GetValue<decimal>());

        Assert.Equal(400, (await usuario.Get("/resumo-mensal?ano=abc")).Status);
    }

    [Fact]
    public async Task Despesas_por_categoria_do_maior_para_o_menor()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var mercado = await usuario.CriarCategoria("Mercado");
        var lazer = await usuario.CriarCategoria("Lazer");
        await usuario.CriarTransacao(mercado, "a", 100, "2030-05-01");
        await usuario.CriarTransacao(lazer, "b", 300, "2030-05-02");
        await usuario.CriarTransacao(mercado, "c", 50, "2030-05-03");
        await usuario.CriarTransacao(lazer, "receita não conta", 999, "2030-05-04", "Receita");

        var lista = (await usuario.Get("/despesas-por-categoria?mes=2030-05")).Dados!.AsArray();
        Assert.Equal(["Lazer", "Mercado"], lista.Select(c => c!["categoria"]!.GetValue<string>()));
        Assert.Equal([300m, 150m], lista.Select(c => c!["total"]!.GetValue<decimal>()));
    }

    [Fact]
    public async Task Metas_do_mes_mostram_o_gasto_e_as_mais_perto_de_estourar_primeiro()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var mercado = await usuario.CriarCategoria("Mercado", 800);
        var lazer = await usuario.CriarCategoria("Lazer", 300);
        await usuario.CriarCategoria("Sem meta");
        await usuario.CriarCategoria("Moradia", 2000);
        await usuario.CriarTransacao(mercado, "a", 640, "2030-06-10");
        await usuario.CriarTransacao(mercado, "outro mês", 999, "2030-07-01");
        await usuario.CriarTransacao(lazer, "b", 350, "2030-06-15");

        var metas = (await usuario.Get("/metas?mes=2030-06")).Dados!.AsArray();
        Assert.Equal(["Lazer", "Mercado", "Moradia"], metas.Select(m => m!["categoria"]!.GetValue<string>()));
        Assert.Equal([350m, 640m, 0m], metas.Select(m => m!["gasto"]!.GetValue<decimal>()));
        Assert.Equal(400, (await usuario.Get("/metas")).Status); // o mês é obrigatório
    }

    [Fact]
    public async Task Pendentes_trazem_as_atrasadas_e_os_totais()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        await usuario.CriarTransacao(cat, "Atrasada de antes", 100, "2020-01-10", pago: false);
        await usuario.CriarTransacao(cat, "Paga", 999, "2020-02-10", pago: true);
        await usuario.CriarTransacao(cat, "Do mês", 50, "2020-02-20", pago: false);
        await usuario.CriarTransacao(cat, "A receber", 700, "2020-02-25", "Receita", pago: false);
        await usuario.CriarTransacao(cat, "Mês seguinte", 30, "2020-03-01", pago: false);

        var r = await usuario.Get("/pendentes?mes=2020-02");
        var lista = r.Dados!["transacoes"]!.AsArray();
        Assert.Equal(["Atrasada de antes", "Do mês", "A receber"], lista.Select(t => t!["descricao"]!.GetValue<string>()));
        Assert.All(lista, t => Assert.True(t!["atrasada"]!.GetValue<bool>())); // 2020 já passou
        Assert.Equal(150m, r.Decimal("aPagar"));
        Assert.Equal(700m, r.Decimal("aReceber"));

        var futura = await usuario.CriarTransacao(cat, "Futura", 10, "2099-01-01", pago: false);
        var todas = (await usuario.Get("/pendentes")).Dados!["transacoes"]!.AsArray();
        var futuraNaLista = todas.Single(t => t!["id"]!.GetValue<int>() == futura["id"]!.GetValue<int>());
        Assert.False(futuraNaLista!["atrasada"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Relatorios_nao_misturam_usuarios()
    {
        var ana = await Cliente.NovoUsuario(api);
        var bruno = await Cliente.NovoUsuario(api);
        var cat = await ana.CriarCategoria("Lazer", 100);
        await ana.CriarTransacao(cat, "x", 500, "2030-08-01", pago: false);

        Assert.Equal(0m, (await bruno.Get("/saldo")).Decimal("saldo"));
        Assert.Empty((await bruno.Get("/pendentes")).Dados!["transacoes"]!.AsArray());
        Assert.Empty((await bruno.Get("/metas?mes=2030-08")).Dados!.AsArray());
        Assert.Empty((await bruno.Get("/despesas-por-categoria")).Dados!.AsArray());
        Assert.Empty((await bruno.Get("/resumo-anual?ano=2030")).Dados!["categorias"]!.AsArray());
        Assert.Equal(0m, (await bruno.Get("/resumo-anual?ano=2030")).Dados!["atual"]!["despesas"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Resumo_anual_de_ano_fechado_compara_os_12_meses_e_as_categorias()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var mercado = await usuario.CriarCategoria("Mercado");
        var transporte = await usuario.CriarCategoria("Transporte");
        var lazer = await usuario.CriarCategoria("Lazer");
        await usuario.CriarTransacao(mercado, "Salário 2030", 5000, "2030-01-05", "Receita");
        await usuario.CriarTransacao(mercado, "Mercado 2030", 1000, "2030-12-31");
        await usuario.CriarTransacao(transporte, "Ônibus 2030", 400, "2030-06-10");
        await usuario.CriarTransacao(mercado, "Salário 2031", 6000.50m, "2031-01-05", "Receita");
        await usuario.CriarTransacao(mercado, "Mercado 2031", 1200, "2031-12-31"); // dezembro conta (ano fechado)
        await usuario.CriarTransacao(lazer, "Cinema 2031", 50.25m, "2031-03-01");
        await usuario.CriarTransacao(mercado, "Fora (2029)", 999, "2029-12-31");
        await usuario.CriarTransacao(mercado, "Fora (2032)", 999, "2032-01-01");

        var r = (await usuario.Get("/resumo-anual?ano=2031")).Dados!;
        Assert.Equal(2031, r["ano"]!.GetValue<int>());
        Assert.Equal(12, r["ateMes"]!.GetValue<int>());
        decimal Valor(string ano, string campo) => r[ano]![campo]!.GetValue<decimal>();
        Assert.Equal(6000.50m, Valor("atual", "receitas"));
        Assert.Equal(1250.25m, Valor("atual", "despesas"));
        Assert.Equal(4750.25m, Valor("atual", "saldo"));
        Assert.Equal(5000m, Valor("anterior", "receitas"));
        Assert.Equal(1400m, Valor("anterior", "despesas"));
        Assert.Equal(3600m, Valor("anterior", "saldo"));

        // Só despesas; as que mais mudaram em reais primeiro: Transporte (−400), Mercado (+200), Lazer (+50,25)
        var categorias = r["categorias"]!.AsArray();
        Assert.Equal(["Transporte", "Mercado", "Lazer"], categorias.Select(c => c!["categoria"]!.GetValue<string>()));
        Assert.Equal([0m, 1200m, 50.25m], categorias.Select(c => c!["atual"]!.GetValue<decimal>()));
        Assert.Equal([400m, 1000m, 0m], categorias.Select(c => c!["anterior"]!.GetValue<decimal>()));

        Assert.Equal(400, (await usuario.Get("/resumo-anual?ano=abc")).Status);
        Assert.Equal(400, (await usuario.Get("/resumo-anual?ano=1800")).Status);
    }

    [Fact]
    public async Task Resumo_anual_do_ano_atual_compara_so_ate_o_mes_atual()
    {
        var hoje = DateTime.Today;
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        // Último dia do mês atual, um ano atrás: entra na comparação.
        var fimDoMesAnoPassado = new DateTime(hoje.Year - 1, hoje.Month, 1).AddMonths(1).AddDays(-1);
        await usuario.CriarTransacao(cat, "Entra", 100, fimDoMesAnoPassado.ToString("yyyy-MM-dd"));
        await usuario.CriarTransacao(cat, "Este ano", 30, $"{hoje.Year}-01-01");
        // O mês seguinte (do ano passado e deste ano) fica de fora, porque este ano ainda não chegou lá.
        if (hoje.Month < 12)
        {
            await usuario.CriarTransacao(cat, "Fora ano passado", 500, $"{hoje.Year - 1}-{hoje.Month + 1:00}-01");
            await usuario.CriarTransacao(cat, "Fora futuro", 700, $"{hoje.Year}-{hoje.Month + 1:00}-01");
        }

        var r = (await usuario.Get("/resumo-anual")).Dados!; // sem ?ano= usa o ano atual
        Assert.Equal(hoje.Year, r["ano"]!.GetValue<int>());
        Assert.Equal(hoje.Month, r["ateMes"]!.GetValue<int>());
        Assert.Equal(100m, r["anterior"]!["despesas"]!.GetValue<decimal>());
        Assert.Equal(30m, r["atual"]!["despesas"]!.GetValue<decimal>());
    }
}
