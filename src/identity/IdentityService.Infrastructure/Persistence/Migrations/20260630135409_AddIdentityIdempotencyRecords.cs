using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityIdempotencyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "jsonb", nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                    table.CheckConstraint("ck_identity_idempotency_records_status", "status IN ('Processing', 'Completed', 'Failed', 'Expired')");
                });

            migrationBuilder.CreateIndex(
                name: "idx_identity_idempotency_records_expires_at",
                schema: "identity",
                table: "idempotency_records",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_identity_idempotency_records_status",
                schema: "identity",
                table: "idempotency_records",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_identity_idempotency_records_operation_key",
                schema: "identity",
                table: "idempotency_records",
                columns: new[] { "operation_name", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "identity");
        }
    }
}
