using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Models;

public class Usuario
{
    public int Id { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public string SenhaHash { get; set; } = string.Empty;

    // Quanto o usuário quer guardar por mês (receitas − despesas). Vazio = sem meta.
    [Precision(10, 2)]
    public decimal? MetaEconomiaMensal { get; set; }

    // Só no site de demonstração: conta de visitante, apagada depois desta data (UTC).
    // Vazio = usuário de verdade (nunca é apagado).
    public DateTime? VisitanteAte { get; set; }
}
