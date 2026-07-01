using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransferService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTransferSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "transfer");

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "transfer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_merchant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_body = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "transfer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    topic = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    message_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lock_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transferencias_sagas",
                schema: "transfer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_merchant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination_merchant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_step = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    debit_lancamento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_lancamento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    compensation_estorno_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    idempotency_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_lock_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    processing_locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    debit_created = table.Column<bool>(type: "boolean", nullable: false),
                    credit_created = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias_sagas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_transfer_idempotency_expires_at",
                schema: "transfer",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ux_transfer_idempotency_source_key",
                schema: "transfer",
                table: "idempotency_records",
                columns: new[] { "source_merchant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_transfer_outbox_pending",
                schema: "transfer",
                table: "outbox_messages",
                columns: new[] { "status", "next_retry_at", "locked_until" });

            migrationBuilder.CreateIndex(
                name: "idx_transferencias_sagas_worker_pending",
                schema: "transfer",
                table: "transferencias_sagas",
                columns: new[] { "status", "next_retry_at", "processing_locked_until" });

            migrationBuilder.CreateIndex(
                name: "ux_transferencias_sagas_source_idempotency_key",
                schema: "transfer",
                table: "transferencias_sagas",
                columns: new[] { "source_merchant_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "transfer");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "transfer");

            migrationBuilder.DropTable(
                name: "transferencias_sagas",
                schema: "transfer");
        }
    }
}
