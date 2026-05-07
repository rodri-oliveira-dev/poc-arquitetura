using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusTrackingReprocessamentosLancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "reprocessamentos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "failed_at",
                table: "reprocessamentos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "reprocessamentos_lancamentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "processing_started_at",
                table: "reprocessamentos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                table: "reprocessamentos_lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "reprocessamentos_lancamentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "reprocessamentos_lancamentos");

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "reprocessamentos_lancamentos");

            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "reprocessamentos_lancamentos");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "reprocessamentos_lancamentos");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "reprocessamentos_lancamentos");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "reprocessamentos_lancamentos");
        }
    }
}
