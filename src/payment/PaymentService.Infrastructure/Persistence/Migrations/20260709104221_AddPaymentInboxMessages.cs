using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    processing_started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lock_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locked_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_payment_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_payment_inbox_received_at",
                schema: "payment",
                table: "inbox_messages",
                column: "received_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_payment_inbox_status_next_retry",
                schema: "payment",
                table: "inbox_messages",
                columns: new[] { "status", "next_retry_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_inbox_provider_event",
                schema: "payment",
                table: "inbox_messages",
                columns: new[] { "provider", "provider_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "payment");
        }
    }
}
