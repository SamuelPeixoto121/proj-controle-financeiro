using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Models;

public class Transacao
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;

    [Precision(10, 2)]
    public decimal Valor { get; set; }

    public DateTime Data { get; set; }
    public TipoTransacao Tipo { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    // Dono da transação. Fica vazio só nos dados antigos, até o primeiro cadastro.
    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
