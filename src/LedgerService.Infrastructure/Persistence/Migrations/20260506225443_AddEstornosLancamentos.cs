using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstornosLancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "estornos_lancamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lancamento_original_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "text", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estornos_lancamentos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_estornos_lancamentos_original",
                table: "estornos_lancamentos",
                column: "lancamento_original_id");

            migrationBuilder.CreateIndex(
                name: "idx_estornos_lancamentos_original_status",
                table: "estornos_lancamentos",
                columns: new[] { "lancamento_original_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estornos_lancamentos");
        }
    }
}
