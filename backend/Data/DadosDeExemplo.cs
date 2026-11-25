using ControleFinanceiro.Models;

namespace ControleFinanceiro.Data;

// Dados inventados para a conta de visitante do site de demonstração:
// do começo do ano passado até o mês que vem, para todas as telas terem o que mostrar
// (resumo mês a mês, comparação com o ano anterior, cartão, contas, metas e relatório).
public static class DadosDeExemplo
{
    public static async Task Criar(AppDbContext db, int usuarioId, DateTime hoje)
    {
        var sorteio = new Random(2026); // sempre os mesmos "sorteios": todo visitante vê os mesmos números
        decimal Entre(int minimo, int maximo) => Math.Round(minimo + (decimal)sorteio.NextDouble() * (maximo - minimo), 2);

        // A conta corrente é criada primeiro: a mais antiga é a conta principal.
        var corrente = new Conta { Nome = "Conta corrente", SaldoInicial = 1200, UsuarioId = usuarioId };
        var poupanca = new Conta { Nome = "Poupança", SaldoInicial = 3000, UsuarioId = usuarioId };
        db.Contas.Add(corrente);
        await db.SaveChangesAsync();
        db.Contas.Add(poupanca);

        Categoria Categoria(string nome, decimal? meta = null)
        {
            var categoria = new Categoria { Nome = nome, MetaMensal = meta, UsuarioId = usuarioId };
            db.Categorias.Add(categoria);
            return categoria;
        }
        var salario = Categoria("Salário");
        var freelas = Categoria("Freelas");
        var moradia = Categoria("Moradia", 1600);
        var mercado = Categoria("Mercado", 900);
        var transporte = Categoria("Transporte", 300);
        var lazer = Categoria("Lazer", 350);
        var saude = Categoria("Saúde");
        var assinaturas = Categoria("Assinaturas", 80);
        var compras = Categoria("Compras");

        var usuario = await db.Usuarios.FindAsync(usuarioId);
        usuario!.MetaEconomiaMensal = 800;

        var cartao = new Cartao { Nome = "Cartão de crédito", DiaFechamento = 3, DiaVencimento = 10, UsuarioId = usuarioId };
        db.Cartoes.Add(cartao);
        await db.SaveChangesAsync(); // para as contas, categorias e o cartão ganharem Id

        var transacoes = new List<Transacao>();
        void Lancar(string descricao, decimal valor, DateTime data, Categoria categoria,
            TipoTransacao tipo = TipoTransacao.Despesa, Guid? serie = null, DateTime? compraNoCartao = null) =>
            transacoes.Add(new Transacao
            {
                Descricao = descricao,
                Valor = valor,
                Data = data,
                Tipo = tipo,
                CategoriaId = categoria.Id,
                Pago = data <= hoje, // o que já venceu está pago; o resto fica pendente
                SerieId = serie,
                CartaoId = compraNoCartao is null ? null : cartao.Id,
                DataCompra = compraNoCartao,
                ContaId = corrente.Id,
                UsuarioId = usuarioId,
            });
        void NoCartao(string descricao, decimal valor, DateTime compra, Categoria categoria) =>
            Lancar(descricao, valor, cartao.VencimentoDaFatura(compra), categoria, compraNoCartao: compra);

        Guid serieSalario = Guid.NewGuid(), serieAluguel = Guid.NewGuid(), serieInternet = Guid.NewGuid();
        var inicio = new DateTime(hoje.Year - 1, 1, 1);
        var fim = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1);
        for (var mes = inicio; mes <= fim; mes = mes.AddMonths(1))
        {
            var anoPassado = mes.Year < hoje.Year; // no ano passado, ganhava e gastava um pouco menos
            DateTime Dia(int dia) => mes.AddDays(dia - 1);

            Lancar("Salário", anoPassado ? 4200 : 4500, Dia(5), salario, TipoTransacao.Receita, serieSalario);
            if (mes.Month % 2 == 0)
                Lancar("Projeto freela", Entre(400, 1100), Dia(25), freelas, TipoTransacao.Receita);

            Lancar("Aluguel", anoPassado ? 1350 : 1450, Dia(10), moradia, serie: serieAluguel);
            Lancar("Conta de luz", Entre(130, 230), Dia(15), moradia);
            Lancar("Internet", 99.90m, Dia(20), moradia, serie: serieInternet);
            Lancar("Supermercado", Entre(280, 480), Dia(8), mercado);
            Lancar("Supermercado", Entre(250, 450), Dia(22), mercado);
            Lancar(mes.Month % 3 == 0 ? "Combustível" : "Uber", Entre(150, 320), Dia(12), transporte);
            if (mes.Month % 3 == 1)
                Lancar("Farmácia", Entre(60, 150), Dia(17), saude);

            NoCartao("Streaming", 39.90m, Dia(2), assinaturas);
            NoCartao("Música", 21.90m, Dia(2), assinaturas);
            NoCartao(mes.Month % 2 == 0 ? "Restaurante" : "Cinema e lanche", Entre(90, 260), Dia(18), lazer);
            if (mes.Month is 7 or 12)
                NoCartao("Viagem de férias", Entre(700, 1100), Dia(14), lazer);

            if (mes >= fim.AddMonths(-6)) // só nos últimos meses, para a lista não ficar enorme
                db.Transferencias.Add(new Transferencia
                {
                    Descricao = "Guardar na poupança",
                    Valor = 500,
                    Data = Dia(6),
                    ContaOrigemId = corrente.Id,
                    ContaDestinoId = poupanca.Id,
                    UsuarioId = usuarioId,
                });
        }

        // Uma conta que vence daqui a 3 dias: o card "Contas do mês" sempre tem o que mostrar.
        Lancar("Conta de água", 86.40m, hoje.AddDays(3), moradia);

        // Uma compra parcelada no cartão, feita há 4 meses: uma parcela em cada fatura.
        var compraNotebook = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-4).AddDays(19);
        var serieNotebook = Guid.NewGuid();
        for (var i = 0; i < 10; i++)
            Lancar($"Notebook ({i + 1}/10)", 360, cartao.VencimentoDaFatura(compraNotebook).AddMonths(i), compras,
                serie: serieNotebook, compraNoCartao: compraNotebook);

        db.Transacoes.AddRange(transacoes);
        await db.SaveChangesAsync();
    }
}
