using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Models;

// Dinheiro que sai de uma conta e entra em outra. Não é receita nem despesa:
// só muda o saldo das duas contas (e só depois que a data chega).
public class Transferencia
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;

    [Precision(10, 2)]
    public decimal Valor { get; set; }

    public DateTime Data { get; set; }
    public int ContaOrigemId { get; set; }
    public int ContaDestinoId { get; set; }

    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
