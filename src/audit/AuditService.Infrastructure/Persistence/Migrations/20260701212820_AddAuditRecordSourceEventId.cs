using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRecordSourceEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_event_id",
                schema: "audit",
                table: "functional_audit_records",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_audit_functional_audit_records_source_event_id",
                schema: "audit",
                table: "functional_audit_records",
                column: "source_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_audit_functional_audit_records_source_event_id",
                schema: "audit",
                table: "functional_audit_records");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                schema: "audit",
                table: "functional_audit_records");
        }
    }
}
