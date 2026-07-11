using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;

namespace LedgerService.UnitTests.Domain.Entities;

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
                correlationId: Guid.NewGuid(),
                createdAt: DateTime.UtcNow));

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
                correlationId: Guid.NewGuid(),
                createdAt: DateTime.UtcNow));

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
                correlationId: Guid.NewGuid(),
                createdAt: DateTime.UtcNow));

        Assert.Contains("DEBIT", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_create_credit_with_positive_amount()
    {
        var entry = LedgerEntry.RegistrarCredito(
            merchantId: "m1",
            amount: 10m,
            occurredAt: DateTime.UtcNow,
            description: null,
            externalReference: null,
            correlationId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow);

        Assert.Equal(LedgerEntryType.Credit, entry.Type);
        Assert.Equal(10m, entry.Amount);
    }

    [Fact]
    public void Should_create_debit_with_negative_amount()
    {
        var entry = LedgerEntry.RegistrarDebito(
            merchantId: "m1",
            amount: -10m,
            occurredAt: DateTime.UtcNow,
            description: null,
            externalReference: null,
            correlationId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow);

        Assert.Equal(LedgerEntryType.Debit, entry.Type);
        Assert.Equal(-10m, entry.Amount);
    }

    [Fact]
    public void Should_throw_when_merchant_id_is_empty()
    {
        var ex = Assert.Throws<DomainException>(() =>
            LedgerEntry.RegistrarCredito(
                merchantId: " ",
                amount: 10m,
                occurredAt: DateTime.UtcNow,
                description: null,
                externalReference: null,
                correlationId: Guid.NewGuid(),
                createdAt: DateTime.UtcNow));

        Assert.Contains("MerchantId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_create_compensating_entry_inverting_credit_amount_and_preserving_original_reference()
    {
        var original = LedgerEntry.RegistrarCredito(
            "m1",
            100m,
            DateTime.UtcNow.AddMinutes(-5),
            "Venda",
            "ext",
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-5));
        var correlationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var compensating = original.CriarLancamentoCompensatorio(correlationId, "Erro operacional", now);

        Assert.Equal(LedgerEntryType.Debit, compensating.Type);
        Assert.Equal(-100m, compensating.Amount);
        Assert.Equal(original.MerchantId, compensating.MerchantId);
        Assert.Equal($"estorno:{original.Id:N}", compensating.ExternalReference);
        Assert.Equal(correlationId, compensating.CorrelationId);
        Assert.Contains(original.Id.ToString(), compensating.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_create_compensating_entry_inverting_debit_amount()
    {
        var original = LedgerEntry.RegistrarDebito(
            "m1",
            -75m,
            DateTime.UtcNow.AddMinutes(-5),
            "Compra",
            "ext",
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-5));

        var compensating = original.CriarLancamentoCompensatorio(Guid.NewGuid(), "Erro operacional", DateTime.UtcNow);

        Assert.Equal(LedgerEntryType.Credit, compensating.Type);
        Assert.Equal(75m, compensating.Amount);
        Assert.Equal($"estorno:{original.Id:N}", compensating.ExternalReference);
    }
}
