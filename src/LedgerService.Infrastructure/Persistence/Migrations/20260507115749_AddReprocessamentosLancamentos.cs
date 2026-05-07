using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReprocessamentosLancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reprocessamentos_lancamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_inicial = table.Column<DateOnly>(type: "date", nullable: false),
                    data_final = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reprocessamentos_lancamentos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_reprocessamentos_lancamentos_merchant_periodo",
                table: "reprocessamentos_lancamentos",
                columns: new[] { "merchant_id", "data_inicial", "data_final" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reprocessamentos_lancamentos");
        }
    }
}
