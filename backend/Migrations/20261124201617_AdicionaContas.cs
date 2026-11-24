using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaContas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContaId",
                table: "Transacoes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContaId",
                table: "Cartoes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Contas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SaldoInicial = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Transferencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Descricao = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ContaOrigemId = table.Column<int>(type: "int", nullable: false),
                    ContaDestinoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transferencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transferencias_Contas_ContaDestinoId",
                        column: x => x.ContaDestinoId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transferencias_Contas_ContaOrigemId",
                        column: x => x.ContaOrigemId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transferencias_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_ContaId",
                table: "Transacoes",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cartoes_ContaId",
                table: "Cartoes",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Contas_UsuarioId",
                table: "Contas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Transferencias_ContaDestinoId",
                table: "Transferencias",
                column: "ContaDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Transferencias_ContaOrigemId",
                table: "Transferencias",
                column: "ContaOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_Transferencias_UsuarioId",
                table: "Transferencias",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cartoes_Contas_ContaId",
                table: "Cartoes",
                column: "ContaId",
                principalTable: "Contas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_Contas_ContaId",
                table: "Transacoes",
                column: "ContaId",
                principalTable: "Contas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Cada usuário que já existe ganha a "Conta principal",
            // e as transações e cartões dele passam a ser dessa conta.
            migrationBuilder.Sql(
                "INSERT INTO Contas (Nome, SaldoInicial, UsuarioId) SELECT 'Conta principal', 0, Id FROM Usuarios;");
            migrationBuilder.Sql(
                "UPDATE Transacoes t JOIN Contas c ON c.UsuarioId = t.UsuarioId SET t.ContaId = c.Id;");
            migrationBuilder.Sql(
                "UPDATE Cartoes k JOIN Contas c ON c.UsuarioId = k.UsuarioId SET k.ContaId = c.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cartoes_Contas_ContaId",
                table: "Cartoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_Contas_ContaId",
                table: "Transacoes");

            migrationBuilder.DropTable(
                name: "Transferencias");

            migrationBuilder.DropTable(
                name: "Contas");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_ContaId",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_Cartoes_ContaId",
                table: "Cartoes");

            migrationBuilder.DropColumn(
                name: "ContaId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "ContaId",
                table: "Cartoes");
        }
    }
}
