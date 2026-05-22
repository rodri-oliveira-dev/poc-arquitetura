using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDeadLetterBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_outbox_pending",
                table: "outbox_messages");

            migrationBuilder.RenameColumn(
                name: "attempts",
                table: "outbox_messages",
                newName: "retry_count");

            migrationBuilder.RenameColumn(
                name: "next_attempt_at",
                table: "outbox_messages",
                newName: "next_retry_at");

            migrationBuilder.Sql("UPDATE outbox_messages SET status = 'Processed' WHERE status = 'Sent';");
            migrationBuilder.Sql("UPDATE outbox_messages SET status = 'DeadLetter' WHERE status = 'Failed';");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_pending",
                table: "outbox_messages",
                columns: new[] { "status", "next_retry_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_outbox_pending",
                table: "outbox_messages");

            migrationBuilder.Sql("UPDATE outbox_messages SET status = 'Sent' WHERE status = 'Processed';");
            migrationBuilder.Sql("UPDATE outbox_messages SET status = 'Failed' WHERE status = 'DeadLetter';");

            migrationBuilder.RenameColumn(
                name: "retry_count",
                table: "outbox_messages",
                newName: "attempts");

            migrationBuilder.RenameColumn(
                name: "next_retry_at",
                table: "outbox_messages",
                newName: "next_attempt_at");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_pending",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });
        }
    }
}
