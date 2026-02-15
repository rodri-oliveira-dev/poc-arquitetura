using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class LedgerEntry : Entity, IAggregateRoot
{
    public string MerchantId { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? Description { get; private set; }
    public string? ExternalReference { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LedgerEntry()
    {
        MerchantId = string.Empty;
    }

    public LedgerEntry(
        string merchantId,
        LedgerEntryType type,
        decimal amount,
        DateTime occurredAt,
        string? description,
        string? externalReference,
        Guid correlationId)
    {
        MerchantId = string.IsNullOrWhiteSpace(merchantId)
            ? throw new DomainException("MerchantId é obrigatório.")
            : merchantId.Trim();

        EnsureValidAmount(type, amount);

        Type = type;
        Amount = amount;
        OccurredAt = occurredAt;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
        CorrelationId = correlationId;
        CreatedAt = DateTime.Now;
    }

    private static void EnsureValidAmount(LedgerEntryType type, decimal amount)
    {
        if (amount == 0)
            throw new DomainException("Amount não pode ser zero.");

        if (type == LedgerEntryType.Credit && amount < 0)
            throw new DomainException("Para Type=CREDIT, Amount deve ser positivo.");

        if (type == LedgerEntryType.Debit && amount > 0)
            throw new DomainException("Para Type=DEBIT, Amount deve ser negativo.");
    }
}