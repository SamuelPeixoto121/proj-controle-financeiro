using System.ComponentModel.DataAnnotations;

namespace ControleFinanceiro.Models;

public class Usuario
{
    public int Id { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public string SenhaHash { get; set; } = string.Empty;
}
