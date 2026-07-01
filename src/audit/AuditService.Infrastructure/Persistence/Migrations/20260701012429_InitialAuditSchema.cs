using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "functional_audit_records",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    merchant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    actor_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    actor_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    actor_client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    metadata = table.Column<IReadOnlyDictionary<string, string>>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_functional_audit_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_functional_audit_records_correlation_id",
                schema: "audit",
                table: "functional_audit_records",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_functional_audit_records_entity",
                schema: "audit",
                table: "functional_audit_records",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "idx_audit_functional_audit_records_idempotency_key",
                schema: "audit",
                table: "functional_audit_records",
                column: "idempotency_key");

            migrationBuilder.CreateIndex(
                name: "idx_audit_functional_audit_records_merchant_occurred_at",
                schema: "audit",
                table: "functional_audit_records",
                columns: new[] { "merchant_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_audit_functional_audit_records_operation_id",
                schema: "audit",
                table: "functional_audit_records",
                column: "operation_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_functional_audit_records_source_operation",
                schema: "audit",
                table: "functional_audit_records",
                columns: new[] { "source_service", "operation_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "functional_audit_records",
                schema: "audit");
        }
    }
}
