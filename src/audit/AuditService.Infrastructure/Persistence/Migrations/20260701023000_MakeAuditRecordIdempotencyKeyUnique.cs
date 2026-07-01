using AuditService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditService.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(AuditDbContext))]
[Migration("20260701023000_MakeAuditRecordIdempotencyKeyUnique")]
public partial class MakeAuditRecordIdempotencyKeyUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_audit_functional_audit_records_idempotency_key",
            schema: "audit",
            table: "functional_audit_records");

        migrationBuilder.CreateIndex(
            name: "ux_audit_functional_audit_records_idempotency_key",
            schema: "audit",
            table: "functional_audit_records",
            column: "idempotency_key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_audit_functional_audit_records_idempotency_key",
            schema: "audit",
            table: "functional_audit_records");

        migrationBuilder.CreateIndex(
            name: "idx_audit_functional_audit_records_idempotency_key",
            schema: "audit",
            table: "functional_audit_records",
            column: "idempotency_key");
    }
}
