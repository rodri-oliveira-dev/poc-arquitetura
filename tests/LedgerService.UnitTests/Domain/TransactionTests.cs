using LedgerService.Domain.Common;
using LedgerService.Domain.Transactions;

namespace LedgerService.UnitTests.Domain;

public class TransactionTests
{
    [Fact]
    public void Create_ShouldCreateValidTransaction()
    {
        var now = new DateTimeOffset(2026, 2, 11, 12, 0, 0, TimeSpan.Zero);
        var occurredAt = new DateTimeOffset(2026, 2, 10, 8, 30, 0, TimeSpan.Zero);

        var transaction = Transaction.Create(
            merchantId: "merchant-123",
            type: TransactionType.Credit,
            amount: 120.50m,
            occurredAt: occurredAt,
            now: now);

        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal("merchant-123", transaction.MerchantId);
        Assert.Equal(TransactionType.Credit, transaction.Type);
        Assert.Equal(120.50m, transaction.Amount);
        Assert.Equal(occurredAt, transaction.OccurredAt);
        Assert.Equal(now, transaction.CreatedAt);
    }

    [Fact]
    public void Create_ShouldThrow_WhenMerchantIdIsEmpty()
    {
        var occurredAt = new DateTimeOffset(2026, 2, 10, 8, 30, 0, TimeSpan.Zero);

        Assert.Throws<DomainException>(() =>
            Transaction.Create(
                merchantId: " ",
                type: TransactionType.Credit,
                amount: 10m,
                occurredAt: occurredAt));
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsZeroOrNegative()
    {
        var occurredAt = new DateTimeOffset(2026, 2, 10, 8, 30, 0, TimeSpan.Zero);

        Assert.Throws<DomainException>(() =>
            Transaction.Create(
                merchantId: "merchant-123",
                type: TransactionType.Credit,
                amount: 0m,
                occurredAt: occurredAt));

        Assert.Throws<DomainException>(() =>
            Transaction.Create(
                merchantId: "merchant-123",
                type: TransactionType.Credit,
                amount: -1m,
                occurredAt: occurredAt));
    }

    [Fact]
    public void Create_ShouldThrow_WhenOccurredAtIsDefault()
    {
        Assert.Throws<DomainException>(() =>
            Transaction.Create(
                merchantId: "merchant-123",
                type: TransactionType.Credit,
                amount: 10m,
                occurredAt: default));
    }

    [Fact]
    public void Create_ShouldThrow_WhenTypeIsInvalid()
    {
        var occurredAt = new DateTimeOffset(2026, 2, 10, 8, 30, 0, TimeSpan.Zero);

        Assert.Throws<DomainException>(() =>
            Transaction.Create(
                merchantId: "merchant-123",
                type: (TransactionType)999,
                amount: 10m,
                occurredAt: occurredAt));
    }
}
