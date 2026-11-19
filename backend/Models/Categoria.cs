using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    // Limite de gasto por mês. Vazio = categoria sem meta.
    [Precision(10, 2)]
    public decimal? MetaMensal { get; set; }

    // Dono da categoria. Fica vazio só nos dados antigos, até o primeiro cadastro.
    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
