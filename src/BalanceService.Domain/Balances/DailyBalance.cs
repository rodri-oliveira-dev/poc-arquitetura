using BalanceService.Domain.Common;
using BalanceService.Domain.Exceptions;
using System.Globalization;

namespace BalanceService.Domain.Balances;

/// <summary>
/// Consolidado diário por Merchant + Data + Moeda.
/// </summary>
public sealed class DailyBalance : Entity, IAggregateRoot
{
    public string MerchantId { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    public decimal TotalCredits { get; private set; }
    public decimal TotalDebits { get; private set; }
    public decimal NetBalance { get; private set; }

    public DateTimeOffset AsOf { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // EF Core
    private DailyBalance() { }

    public DailyBalance(string merchantId, DateOnly date, string currency, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            throw new DomainException("MerchantId is required.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException("Currency must be a 3-letter code.");

        MerchantId = merchantId;
        Date = date;
        Currency = currency.Trim().ToUpperInvariant();

        TotalCredits = 0m;
        TotalDebits = 0m;
        NetBalance = 0m;
        AsOf = DateTimeOffset.MinValue;
        UpdatedAt = now;
    }

    public void Apply(LedgerEntryCreatedEvent evt, DateTimeOffset now)
    {
        if (evt is null)
            throw new DomainException("Event is required.");

        if (!decimal.TryParse(evt.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
            throw new DomainException("Invalid amount format.");

        if (parsedAmount == 0m)
            throw new DomainException("Amount cannot be 0.");

        // Regra: CREDIT soma em total_credits; DEBIT soma em total_debits.
        // Observação: no LedgerService, o DEBIT tende a ser negativo. Para o consolidado,
        // total_debits deve ser magnitude positiva.
        var type = evt.Type.Trim().ToUpperInvariant();
        switch (type)
        {
            case "CREDIT":
                TotalCredits += Math.Abs(parsedAmount);
                break;
            case "DEBIT":
                TotalDebits += Math.Abs(parsedAmount);
                break;
            default:
                throw new DomainException("Type must be CREDIT or DEBIT.");
        }

        NetBalance = TotalCredits - TotalDebits;

        if (evt.OccurredAt > AsOf)
            AsOf = evt.OccurredAt;

        UpdatedAt = now;
    }
}
