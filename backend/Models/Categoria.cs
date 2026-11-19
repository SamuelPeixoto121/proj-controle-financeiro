using System.Text.Json.Serialization;

namespace ControleFinanceiro.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    // Dono da categoria. Fica vazio só nos dados antigos, até o primeiro cadastro.
    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
