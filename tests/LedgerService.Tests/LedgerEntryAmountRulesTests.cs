using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Tests;

public sealed class LedgerEntryAmountRulesTests
{
    [Fact]
    public void Should_throw_when_amount_is_zero()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new LedgerEntry(
                merchantId: "m1",
                type: LedgerEntryType.Credit,
                amount: 0m,
                occurredAt: DateTime.UtcNow,
                description: null,
                externalReference: null,
                correlationId: Guid.NewGuid()));

        Assert.Contains("zero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_throw_when_credit_has_negative_amount()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new LedgerEntry(
                merchantId: "m1",
                type: LedgerEntryType.Credit,
                amount: -10m,
                occurredAt: DateTime.UtcNow,
                description: null,
                externalReference: null,
                correlationId: Guid.NewGuid()));

        Assert.Contains("CREDIT", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("positivo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_throw_when_debit_has_positive_amount()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new LedgerEntry(
                merchantId: "m1",
                type: LedgerEntryType.Debit,
                amount: 10m,
                occurredAt: DateTime.UtcNow,
                description: null,
                externalReference: null,
                correlationId: Guid.NewGuid()));

        Assert.Contains("DEBIT", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_create_credit_with_positive_amount()
    {
        var entry = new LedgerEntry(
            merchantId: "m1",
            type: LedgerEntryType.Credit,
            amount: 10m,
            occurredAt: DateTime.UtcNow,
            description: null,
            externalReference: null,
            correlationId: Guid.NewGuid());

        Assert.Equal(LedgerEntryType.Credit, entry.Type);
        Assert.True(entry.Amount > 0);
    }

    [Fact]
    public void Should_create_debit_with_negative_amount()
    {
        var entry = new LedgerEntry(
            merchantId: "m1",
            type: LedgerEntryType.Debit,
            amount: -10m,
            occurredAt: DateTime.UtcNow,
            description: null,
            externalReference: null,
            correlationId: Guid.NewGuid());

        Assert.Equal(LedgerEntryType.Debit, entry.Type);
        Assert.True(entry.Amount < 0);
    }
}
