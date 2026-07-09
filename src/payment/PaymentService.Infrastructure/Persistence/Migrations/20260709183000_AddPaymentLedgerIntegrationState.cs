using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentLedgerIntegrationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ledger_integration_status",
                schema: "payment",
                table: "payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<int>(
                name: "ledger_integration_attempt_count",
                schema: "payment",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ledger_next_retry_at_utc",
                schema: "payment",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ledger_last_error",
                schema: "payment",
                table: "payments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ledger_processing_started_at_utc",
                schema: "payment",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ledger_locked_until_utc",
                schema: "payment",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ledger_lock_owner",
                schema: "payment",
                table: "payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ledger_correlation_id",
                schema: "payment",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_payments_ledger_claim",
                schema: "payment",
                table: "payments",
                columns: new[] { "status", "ledger_integration_status", "ledger_next_retry_at_utc", "ledger_locked_until_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_payments_ledger_claim",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_integration_status",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_integration_attempt_count",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_next_retry_at_utc",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_last_error",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_processing_started_at_utc",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_locked_until_utc",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_lock_owner",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ledger_correlation_id",
                schema: "payment",
                table: "payments");
        }
    }
}
