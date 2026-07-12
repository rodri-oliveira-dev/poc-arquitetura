using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_refunds",
                schema: "payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_refund_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ledger_reversal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ledger_reversal_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ledger_reversal_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    ledger_next_retry_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ledger_last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ledger_processing_started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ledger_locked_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ledger_lock_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_refunds", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "payment",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_payment_refunds_ledger_claim",
                schema: "payment",
                table: "payment_refunds",
                columns: new[] { "status", "ledger_reversal_status", "ledger_next_retry_at_utc", "ledger_locked_until_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_payment_refunds_payment_status",
                schema: "payment",
                table: "payment_refunds",
                columns: new[] { "payment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_refunds_provider_refund_id",
                schema: "payment",
                table: "payment_refunds",
                column: "provider_refund_id",
                unique: true,
                filter: "provider_refund_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_refunds",
                schema: "payment");
        }
    }
}
