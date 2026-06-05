using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedgerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ledger");

            migrationBuilder.CreateTable(
                name: "estornos_lancamentos",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lancamento_original_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "text", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    lancamento_compensatorio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estornos_lancamentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_ledger_entries_amount_credit_debit", "(type = 'Credit' AND amount > 0) OR (type = 'Debit' AND amount < 0)");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    traceparent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tracestate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    baggage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lock_owner = table.Column<string>(type: "text", nullable: true),
                    requeue_count = table.Column<int>(type: "integer", nullable: false),
                    last_requeued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_requeued_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_requeue_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reprocessamentos_lancamentos",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_inicial = table.Column<DateOnly>(type: "date", nullable: false),
                    data_final = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reprocessamentos_lancamentos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_estornos_lancamentos_compensatorio",
                schema: "ledger",
                table: "estornos_lancamentos",
                column: "lancamento_compensatorio_id");

            migrationBuilder.CreateIndex(
                name: "idx_estornos_lancamentos_original_status",
                schema: "ledger",
                table: "estornos_lancamentos",
                columns: new[] { "lancamento_original_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_estornos_lancamentos_original_active",
                schema: "ledger",
                table: "estornos_lancamentos",
                column: "lancamento_original_id",
                unique: true,
                filter: "status IN ('Pending', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_records_expires_at",
                schema: "ledger",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_records_merchant_key",
                schema: "ledger",
                table: "idempotency_records",
                columns: new[] { "merchant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ledger_entries_merchant_occurred_at",
                schema: "ledger",
                table: "ledger_entries",
                columns: new[] { "merchant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_estorno_external_reference",
                schema: "ledger",
                table: "ledger_entries",
                column: "external_reference",
                unique: true,
                filter: "external_reference LIKE 'estorno:%'");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_locked_until",
                schema: "ledger",
                table: "outbox_messages",
                column: "locked_until");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_pending",
                schema: "ledger",
                table: "outbox_messages",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "idx_reprocessamentos_lancamentos_merchant_periodo",
                schema: "ledger",
                table: "reprocessamentos_lancamentos",
                columns: new[] { "merchant_id", "data_inicial", "data_final" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estornos_lancamentos",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "reprocessamentos_lancamentos",
                schema: "ledger");
        }
    }
}
