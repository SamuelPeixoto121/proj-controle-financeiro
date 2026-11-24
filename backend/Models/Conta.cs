using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Models;

// Onde o dinheiro está: conta do banco, carteira, poupança...
public class Conta
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    // Quanto já havia na conta quando ela foi cadastrada no app.
    [Precision(10, 2)]
    public decimal SaldoInicial { get; set; }

    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
