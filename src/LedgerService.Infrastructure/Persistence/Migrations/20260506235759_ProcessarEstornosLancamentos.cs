using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcessarEstornosLancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "estornos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "failed_at",
                table: "estornos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "estornos_lancamentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lancamento_compensatorio_id",
                table: "estornos_lancamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "processing_started_at",
                table: "estornos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                table: "estornos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "estornos_lancamentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_estorno_external_reference",
                table: "ledger_entries",
                column: "external_reference",
                unique: true,
                filter: "external_reference LIKE 'estorno:%'");

            migrationBuilder.CreateIndex(
                name: "idx_estornos_lancamentos_compensatorio",
                table: "estornos_lancamentos",
                column: "lancamento_compensatorio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ledger_entries_estorno_external_reference",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "idx_estornos_lancamentos_compensatorio",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "lancamento_compensatorio_id",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "estornos_lancamentos");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "estornos_lancamentos");
        }
    }
}
