using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstornoConcurrencyControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_estornos_lancamentos_original",
                table: "estornos_lancamentos");

            migrationBuilder.CreateIndex(
                name: "ux_estornos_lancamentos_original_active",
                table: "estornos_lancamentos",
                column: "lancamento_original_id",
                unique: true,
                filter: "status IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_estornos_lancamentos_original_active",
                table: "estornos_lancamentos");

            migrationBuilder.CreateIndex(
                name: "idx_estornos_lancamentos_original",
                table: "estornos_lancamentos",
                column: "lancamento_original_id");
        }
    }
}
