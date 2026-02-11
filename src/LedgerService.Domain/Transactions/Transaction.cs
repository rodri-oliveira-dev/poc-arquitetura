using LedgerService.Domain.Common;

namespace LedgerService.Domain.Transactions;

public sealed class Transaction
{
    private Transaction()
    {
    }

    public Guid Id { get; private set; }

    public string MerchantId { get; private set; } = string.Empty;

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Transaction Create(
        string merchantId,
        TransactionType type,
        decimal amount,
        DateTimeOffset occurredAt,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            throw new DomainException("MerchantId cannot be null or empty.");
        }

        if (!Enum.IsDefined(typeof(TransactionType), type))
        {
            throw new DomainException("Transaction type is invalid.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Amount must be greater than zero.");
        }

        if (occurredAt == default)
        {
            throw new DomainException("OccurredAt cannot be default.");
        }

        return new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId.Trim(),
            Type = type,
            Amount = amount,
            OccurredAt = occurredAt,
            CreatedAt = now ?? DateTimeOffset.UtcNow
        };
    }
}
