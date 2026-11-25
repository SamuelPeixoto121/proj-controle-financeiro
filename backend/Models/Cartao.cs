using System.Text.Json.Serialization;

namespace ControleFinanceiro.Models;

// Cartão de crédito. As compras feitas nele entram na fatura e contam no mês do vencimento.
public class Cartao
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    // Dias do mês (1 a 28, para existirem em todos os meses).
    public int DiaFechamento { get; set; }
    public int DiaVencimento { get; set; }

    // Conta que paga a fatura (as compras no cartão saem dela). Vazio = a conta principal.
    public int? ContaId { get; set; }

    [JsonIgnore]
    public int? UsuarioId { get; set; }

    // Em que dia vence a fatura de uma compra feita nesse dia.
    // Compra antes do dia do fechamento: entra na fatura que fecha neste mês; no dia do fechamento
    // ou depois: na fatura seguinte. Se o vencimento é num dia menor que o fechamento
    // (fecha dia 25, vence dia 5), a fatura vence no mês seguinte ao do fechamento.
    // Ex.: fecha dia 3 e vence dia 10. Compra em 01/12 -> vence 10/12; compra em 05/12 -> vence 10/01.
    public DateTime VencimentoDaFatura(DateTime compra)
    {
        var mesDoFechamento = new DateTime(compra.Year, compra.Month, 1);
        if (compra.Day >= DiaFechamento)
            mesDoFechamento = mesDoFechamento.AddMonths(1);
        var mesDoVencimento = DiaVencimento > DiaFechamento ? mesDoFechamento : mesDoFechamento.AddMonths(1);
        return mesDoVencimento.AddDays(DiaVencimento - 1);
    }
}
