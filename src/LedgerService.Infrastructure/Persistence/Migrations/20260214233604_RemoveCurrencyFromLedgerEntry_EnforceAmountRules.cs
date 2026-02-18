using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCurrencyFromLedgerEntry_EnforceAmountRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_amount_positive",
                table: "ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_currency_length",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "ledger_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_amount_credit_debit",
                table: "ledger_entries",
                sql: "(type = 'Credit' AND amount > 0) OR (type = 'Debit' AND amount < 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_amount_credit_debit",
                table: "ledger_entries");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "ledger_entries",
                type: "char(3)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_amount_positive",
                table: "ledger_entries",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_currency_length",
                table: "ledger_entries",
                sql: "char_length(currency) = 3");
        }
    }
}
