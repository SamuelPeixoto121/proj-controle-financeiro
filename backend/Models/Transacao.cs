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

    // Despesa já paga / receita já recebida. Falso = ainda pendente.
    public bool Pago { get; set; }

    // Mesma série = criadas juntas com "Repetir por N meses". Vazio = transação avulsa.
    public Guid? SerieId { get; set; }

    // Compra no cartão de crédito: "Data" é o vencimento da fatura e "DataCompra" é o dia da compra.
    // Vazios = transação comum (conta/dinheiro).
    public int? CartaoId { get; set; }
    public Cartao? Cartao { get; set; }
    public DateTime? DataCompra { get; set; }

    // Conta de onde o dinheiro sai (ou para onde entra). No cartão, a conta que paga a fatura.
    public int? ContaId { get; set; }
    public Conta? Conta { get; set; }

    // Dono da transação. Fica vazio só nos dados antigos, até o primeiro cadastro.
    [JsonIgnore]
    public int? UsuarioId { get; set; }
}
