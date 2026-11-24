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
    }
}
