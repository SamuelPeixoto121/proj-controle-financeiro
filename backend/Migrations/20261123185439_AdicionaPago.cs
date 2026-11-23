using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Pago",
                table: "Transacoes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Transações que já existiam: até hoje = paga/recebida; datas futuras = pendente.
            migrationBuilder.Sql("UPDATE Transacoes SET Pago = 1 WHERE Data <= CURDATE();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pago",
                table: "Transacoes");
        }
    }
}
