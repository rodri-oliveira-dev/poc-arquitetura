using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class LedgerEntry : Entity, IAggregateRoot
{
    public string MerchantId { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? Description { get; private set; }
    public string? ExternalReference { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LedgerEntry()
    {
        MerchantId = string.Empty;
        Currency = string.Empty;
    }

    public LedgerEntry(
        string merchantId,
        LedgerEntryType type,
        decimal amount,
        string currency,
        DateTime occurredAt,
        string? description,
        string? externalReference,
        Guid correlationId)
    {
        MerchantId = string.IsNullOrWhiteSpace(merchantId)
            ? throw new DomainException("MerchantId é obrigatório.")
            : merchantId.Trim();

        if (amount <= 0)
            throw new DomainException("Amount deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException("Currency deve possuir 3 caracteres.");

        Type = type;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        OccurredAt = occurredAt;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
        CorrelationId = correlationId;
        CreatedAt = DateTime.Now;
    }
}