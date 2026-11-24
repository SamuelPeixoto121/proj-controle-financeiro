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

    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
