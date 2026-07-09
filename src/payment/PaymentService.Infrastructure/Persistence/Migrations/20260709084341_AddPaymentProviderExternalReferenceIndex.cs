using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProviderExternalReferenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_payment_payments_provider_external_reference",
                schema: "payment",
                table: "payments",
                columns: new[] { "provider", "external_payment_reference" },
                unique: true,
                filter: "external_payment_reference IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payment_payments_provider_external_reference",
                schema: "payment",
                table: "payments");
        }
    }
}
