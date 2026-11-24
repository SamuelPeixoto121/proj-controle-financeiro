using System.Text.Json.Nodes;
using ControleFinanceiro.Tests.Infra;

namespace ControleFinanceiro.Tests;

// Contas (banco, carteira, poupança), saldo de cada uma e transferências entre elas.
[Collection("api")]
public class ContasTestes(ApiDeTeste api)
{
    private static async Task<int> CriarConta(Cliente usuario, string nome, decimal saldoInicial = 0)
    {
        var r = await usuario.Post("/contas", new { nome, saldoInicial });
        Assert.Equal(201, r.Status);
        return r.Dados!["id"]!.GetValue<int>();
    }

    private static Task<Resposta> Lancar(Cliente usuario, int categoriaId, int? contaId, decimal valor, string data,
        string tipo = "Despesa", bool pago = true, string descricao = "x", int? cartaoId = null) =>
        usuario.Post("/transacoes", new { descricao, valor, data, tipo, categoriaId, pago, contaId, cartaoId });

    // Saldo de cada conta, pelo nome.
    private static async Task<Dictionary<string, decimal>> Saldos(Cliente usuario) =>
        (await usuario.Get("/contas")).Dados!.AsArray()
            .ToDictionary(c => c!["nome"]!.GetValue<string>(), c => c!["saldo"]!.GetValue<decimal>());

    private static async Task<int> Principal(Cliente usuario) =>
        (await usuario.Get("/contas")).Dados!.AsArray()[0]!["id"]!.GetValue<int>();

    [Fact]
    public async Task Usuario_novo_nasce_com_a_conta_principal_e_ela_e_a_conta_padrao()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var contas = (await usuario.Get("/contas")).Dados!.AsArray();
        Assert.Single(contas);
        Assert.Equal("Conta principal", contas[0]!["nome"]!.GetValue<string>());

        var cat = await usuario.CriarCategoria();
        var t = await usuario.CriarTransacao(cat, "Salário", 1000, "2030-01-05", "Receita"); // sem contaId
        Assert.Equal(await Principal(usuario), t["contaId"]!.GetValue<int>());
        Assert.Equal("Conta principal", t["conta"]!["nome"]!.GetValue<string>());
        Assert.Equal(1000m, (await Saldos(usuario))["Conta principal"]);
    }

    [Fact]
    public async Task Saldo_de_cada_conta_conta_so_o_que_foi_pago_e_o_saldo_inicial_entra_no_saldo_real_e_no_acumulado()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var itau = await CriarConta(usuario, "Itaú", 500);
        await Lancar(usuario, cat, itau, 1000, "2030-05-05", "Receita");
        await Lancar(usuario, cat, itau, 200, "2030-05-10");
        await Lancar(usuario, cat, itau, 300, "2030-05-20", pago: false); // pendente: não mexe no saldo da conta
        await Lancar(usuario, cat, null, 50, "2030-05-15");               // conta principal

        var saldos = await Saldos(usuario);
        Assert.Equal(1300m, saldos["Itaú"]);           // 500 + 1000 − 200
        Assert.Equal(-50m, saldos["Conta principal"]);

        var saldo = await usuario.Get("/saldo?mes=2030-05");
        Assert.Equal(1250m, saldo.Decimal("saldoReal")); // = soma das contas
        Assert.Equal(950m, saldo.Decimal("acumulado"));  // 500 + 1000 − 200 − 300 − 50
        Assert.Equal(1000m, saldo.Decimal("receitas"));  // o saldo inicial não é receita

        var meses = (await usuario.Get("/resumo-mensal?ano=2030")).Dados!.AsArray();
        Assert.Equal(500m, meses[0]!["acumulado"]!.GetValue<decimal>()); // janeiro: só o saldo inicial
        Assert.Equal(950m, meses[11]!["acumulado"]!.GetValue<decimal>());

        // Editar o saldo inicial muda o saldo da conta
        Assert.Equal(200, (await usuario.Put($"/contas/{itau}", new { nome = "Itaú PF", saldoInicial = 0 })).Status);
        Assert.Equal(800m, (await Saldos(usuario))["Itaú PF"]);
    }

    [Fact]
    public async Task Transferencia_muda_as_duas_contas_e_nao_e_receita_nem_despesa()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var itau = await CriarConta(usuario, "Itaú", 1000);
        var poupanca = await CriarConta(usuario, "Poupança");

        var r = await usuario.Post("/transferencias", new { descricao = "Guardar", valor = 400, data = "2020-01-10", contaOrigemId = itau, contaDestinoId = poupanca });
        Assert.Equal(201, r.Status);
        var id = r.Dados!["id"]!.GetValue<int>();
        await usuario.Post("/transferencias", new { valor = 100, data = "2099-01-10", contaOrigemId = itau, contaDestinoId = poupanca });

        var saldos = await Saldos(usuario);
        Assert.Equal(600m, saldos["Itaú"]);     // a de 2099 ainda não aconteceu
        Assert.Equal(400m, saldos["Poupança"]);

        var lista = (await usuario.Get("/transferencias")).Dados!.AsArray();
        Assert.Equal([true, false], lista.Select(t => t!["agendada"]!.GetValue<bool>())); // a mais nova primeiro
        Assert.Equal("Itaú", lista[1]!["origem"]!.GetValue<string>());
        Assert.Equal("Poupança", lista[1]!["destino"]!.GetValue<string>());
        Assert.Equal("Guardar", lista[1]!["descricao"]!.GetValue<string>());

        // Não aparece como receita nem despesa em lugar nenhum
        var saldo = await usuario.Get("/saldo?mes=2020-01");
        Assert.Equal(0m, saldo.Decimal("receitas"));
        Assert.Equal(0m, saldo.Decimal("despesas"));
        Assert.Equal(1000m, saldo.Decimal("saldoReal")); // o total não muda
        Assert.Empty((await usuario.Get("/transacoes")).Dados!.AsArray());
        Assert.Empty((await usuario.Get("/despesas-por-categoria?mes=2020-01")).Dados!.AsArray());

        // Excluir desfaz
        Assert.Equal(204, (await usuario.Delete($"/transferencias/{id}")).Status);
        Assert.Equal(1000m, (await Saldos(usuario))["Itaú"]);

        // Validações
        Assert.Equal(400, (await usuario.Post("/transferencias", new { valor = 10, data = "2020-01-10", contaOrigemId = itau, contaDestinoId = itau })).Status);
        Assert.Equal(400, (await usuario.Post("/transferencias", new { valor = 0, data = "2020-01-10", contaOrigemId = itau, contaDestinoId = poupanca })).Status);
        Assert.Equal(400, (await usuario.Post("/transferencias", new { valor = 10, contaOrigemId = itau, contaDestinoId = poupanca })).Status);
    }

    [Fact]
    public async Task Compra_no_cartao_sai_da_conta_que_paga_a_fatura()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var itau = await CriarConta(usuario, "Itaú", 1000);
        var cartaoItau = (await usuario.Post("/cartoes", new { nome = "Itaú Visa", diaFechamento = 3, diaVencimento = 10, contaId = itau })).Dados!;
        Assert.Equal(itau, cartaoItau["contaId"]!.GetValue<int>());
        var semConta = (await usuario.Post("/cartoes", new { nome = "Outro", diaFechamento = 3, diaVencimento = 10 })).Dados!["id"]!.GetValue<int>();

        // Mesmo mandando outra conta, a compra no cartão fica na conta do cartão.
        var compra = await Lancar(usuario, cat, await Principal(usuario), 150, "2020-11-05", pago: false, cartaoId: cartaoItau["id"]!.GetValue<int>());
        Assert.Equal(itau, compra.Dados!["contaId"]!.GetValue<int>());
        var outra = await Lancar(usuario, cat, null, 20, "2020-11-05", pago: false, cartaoId: semConta);
        Assert.Equal(await Principal(usuario), outra.Dados!["contaId"]!.GetValue<int>());

        Assert.Equal(1000m, (await Saldos(usuario))["Itaú"]); // fatura ainda não paga
        await usuario.Put($"/cartoes/{cartaoItau["id"]}/faturas/2020-12-10/pago", new { pago = true });
        Assert.Equal(850m, (await Saldos(usuario))["Itaú"]);
    }

    [Fact]
    public async Task Editar_mantem_ou_troca_a_conta_e_a_serie_leva_a_conta_junto()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var itau = await CriarConta(usuario, "Itaú");
        var t = await usuario.CriarTransacao(cat, "Aluguel", 1000, "2030-01-10", consulta: "?repetir=3");
        var id = t["id"]!.GetValue<int>();

        // Trocar a conta (e aplicar aos meses seguintes)
        var r = await usuario.Put($"/transacoes/{id}?seguintes=true", new { descricao = "Aluguel", valor = 1000, data = "2030-01-10", tipo = "Despesa", categoriaId = cat, pago = true, contaId = itau });
        Assert.Equal(itau, r.Dados!["contaId"]!.GetValue<int>());
        Assert.All(await usuario.Transacoes("Aluguel"), x => Assert.Equal(itau, x["contaId"]!.GetValue<int>()));

        // Sem contaId: continua na mesma conta
        r = await usuario.Put($"/transacoes/{id}", new { descricao = "Aluguel", valor = 1100, data = "2030-01-10", tipo = "Despesa", categoriaId = cat, pago = true });
        Assert.Equal(itau, r.Dados!["contaId"]!.GetValue<int>());
    }

    [Fact]
    public async Task Importar_vai_para_a_conta_escolhida()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var itau = await CriarConta(usuario, "Itaú");
        var itens = new[] { new { linha = 2, descricao = "Pix", valor = 10, data = "2030-01-01", tipo = "Despesa", categoriaId = cat } };

        Assert.Equal(200, (await usuario.Post($"/transacoes/importar?contaId={itau}", itens)).Status);
        Assert.Equal(200, (await usuario.Post("/transacoes/importar", itens)).Status);
        var contas = (await usuario.Transacoes("Pix")).Select(t => t["contaId"]!.GetValue<int>()).Order();
        Assert.Equal(new[] { itau, await Principal(usuario) }.Order(), contas);
    }

    [Fact]
    public async Task Exclusao_de_conta_e_validacoes()
    {
        var usuario = await Cliente.NovoUsuario(api);
        var cat = await usuario.CriarCategoria();
        var principal = await Principal(usuario);

        Assert.Equal(400, (await usuario.Post("/contas", new { nome = " ", saldoInicial = 0 })).Status);
        Assert.Equal(400, (await usuario.Post("/contas", new { nome = "X", saldoInicial = 100_000_000m })).Status);
        Assert.Equal(201, (await usuario.Post("/contas", new { nome = "Cheque especial", saldoInicial = -300 })).Status); // negativo pode

        // Conta vazia: pode excluir
        var vazia = await CriarConta(usuario, "Vazia");
        Assert.Equal(204, (await usuario.Delete($"/contas/{vazia}")).Status);

        // Com transação, cartão ou transferência: não pode
        var comTransacao = await CriarConta(usuario, "Com transação");
        await Lancar(usuario, cat, comTransacao, 10, "2030-01-01");
        Assert.Equal(409, (await usuario.Delete($"/contas/{comTransacao}")).Status);

        var comCartao = await CriarConta(usuario, "Com cartão");
        await usuario.Post("/cartoes", new { nome = "C", diaFechamento = 3, diaVencimento = 10, contaId = comCartao });
        Assert.Equal(409, (await usuario.Delete($"/contas/{comCartao}")).Status);

        var comTransferencia = await CriarConta(usuario, "Com transferência");
        await usuario.Post("/transferencias", new { valor = 5, data = "2020-01-01", contaOrigemId = principal, contaDestinoId = comTransferencia });
        Assert.Equal(409, (await usuario.Delete($"/contas/{comTransferencia}")).Status);

        var sozinho = await Cliente.NovoUsuario(api);
        var r = await sozinho.Delete($"/contas/{await Principal(sozinho)}");
        Assert.Equal(409, r.Status);
        Assert.Equal("Não é possível excluir a última conta.", r.Mensagem);
    }

    [Fact]
    public async Task Conta_de_outro_usuario_nao_aparece_nem_pode_ser_usada()
    {
        var ana = await Cliente.NovoUsuario(api);
        var bruno = await Cliente.NovoUsuario(api);
        var contaDaAna = await CriarConta(ana, "Da Ana", 999);
        var contaDoBruno = await Principal(bruno);
        var catDoBruno = await bruno.CriarCategoria();

        Assert.DoesNotContain("Da Ana", (await Saldos(bruno)).Keys);
        Assert.Equal(404, (await bruno.Put($"/contas/{contaDaAna}", new { nome = "x", saldoInicial = 0 })).Status);
        Assert.Equal(404, (await bruno.Delete($"/contas/{contaDaAna}")).Status);
        Assert.Equal("Conta não encontrada.", (await Lancar(bruno, catDoBruno, contaDaAna, 10, "2030-01-01")).Mensagem);
        Assert.Equal(400, (await bruno.Post("/cartoes", new { nome = "C", diaFechamento = 3, diaVencimento = 10, contaId = contaDaAna })).Status);
        Assert.Equal(400, (await bruno.Post("/transferencias", new { valor = 5, data = "2020-01-01", contaOrigemId = contaDaAna, contaDestinoId = contaDoBruno })).Status);
        Assert.Equal(400, (await bruno.Post($"/transacoes/importar?contaId={contaDaAna}", new[] { new { linha = 2, descricao = "x", valor = 1, data = "2030-01-01", tipo = "Despesa", categoriaId = catDoBruno } })).Status);
        Assert.Empty((await bruno.Get("/transferencias")).Dados!.AsArray());
        Assert.Equal(0m, (await bruno.Get("/saldo")).Decimal("saldoReal")); // saldo inicial da Ana não entra
    }
}
