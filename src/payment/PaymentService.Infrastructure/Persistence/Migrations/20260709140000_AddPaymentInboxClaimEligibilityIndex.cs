using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInboxClaimEligibilityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_payment_inbox_claim_eligibility",
                schema: "payment",
                table: "inbox_messages",
                columns: new[] { "status", "next_retry_at_utc", "locked_until_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_inbox_claim_eligibility",
                schema: "payment",
                table: "inbox_messages");
        }
    }
}
