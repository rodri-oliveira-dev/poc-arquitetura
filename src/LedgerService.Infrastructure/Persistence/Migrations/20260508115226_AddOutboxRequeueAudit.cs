using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRequeueAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_requeue_reason",
                table: "outbox_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_requeued_at",
                table: "outbox_messages",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_requeued_by",
                table: "outbox_messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "requeue_count",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_requeue_reason",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "last_requeued_at",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "last_requeued_by",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "requeue_count",
                table: "outbox_messages");
        }
    }
}
