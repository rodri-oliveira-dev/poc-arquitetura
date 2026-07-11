using System.Globalization;

using BalanceService.Application.IntegrationEvents;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;

namespace BalanceService.UnitTests.Application.IntegrationEvents;

public sealed class LedgerEntryCreatedIntegrationEventMapperTests
{
    [Fact]
    public void Should_map_credit_event_to_balance_movement()
    {
        var evt = Event(type: "CREDIT", amount: "10.00", currency: "brl");

        var movement = LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(evt);

        Assert.Equal(BalanceMovementType.Credit, movement.Type);
        Assert.Equal(10m, movement.Amount.Value);
        Assert.Equal("BRL", movement.Currency.Code);
        Assert.Equal(new DateOnly(2026, 2, 16), movement.Date);
        Assert.Equal(evt.OccurredAt.ToUniversalTime(), movement.OccurredAt);
    }

    [Fact]
    public void Should_map_debit_event_to_balance_movement()
    {
        var movement = LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(Event(type: "DEBIT", amount: "-10.00"));

        Assert.Equal(BalanceMovementType.Debit, movement.Type);
        Assert.Equal(-10m, movement.Amount.Value);
        Assert.Equal(10m, movement.Amount.Magnitude);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public void Should_reject_invalid_amount(string amount)
    {
        BalanceMovement act() => LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(Event(amount: amount));

        Assert.Throws<DomainException>((Func<BalanceMovement>)act);
    }

    [Fact]
    public void Should_reject_zero_amount()
    {
        static BalanceMovement act() => LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(Event(amount: "0.00"));

        var ex = Assert.Throws<DomainException>((Func<BalanceMovement>)act);
        Assert.Contains("Amount", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BR")]
    public void Should_reject_missing_or_invalid_currency(string? currency)
    {
        BalanceMovement act() => LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(Event(currency: currency));

        var ex = Assert.Throws<DomainException>((Func<BalanceMovement>)act);
        Assert.Contains("currency", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_reject_missing_merchant(string merchantId)
    {
        BalanceMovement act() => LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(Event(merchantId: merchantId));

        var ex = Assert.Throws<DomainException>((Func<BalanceMovement>)act);
        Assert.Contains("MerchantId", ex.Message);
    }

    [Fact]
    public void Should_derive_balance_date_from_occurred_at_original_offset()
    {
        var evt = Event(occurredAt: DateTimeOffset.Parse("2026-02-16T23:30:00-03:00", CultureInfo.InvariantCulture));

        var movement = LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(evt);

        Assert.Equal(new DateOnly(2026, 2, 16), movement.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-02-17T02:30:00Z", CultureInfo.InvariantCulture), movement.OccurredAt);
    }

    [Fact]
    public void Should_not_carry_correlation_id_into_domain_movement()
    {
        var movement = LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(
            Event(correlationId: "2cbdd495-586f-4565-a807-c5dc6710d237"));

        Assert.DoesNotContain(
            movement.GetType().GetProperties().Select(property => property.Name),
            propertyName => propertyName.Contains("Correlation", StringComparison.OrdinalIgnoreCase));
    }

    private static LedgerEntryCreatedIntegrationEvent Event(
        string type = "CREDIT",
        string amount = "10.00",
        string? currency = "BRL",
        string merchantId = "merchant-1",
        DateTimeOffset? occurredAt = null,
        string correlationId = "2cbdd495-586f-4565-a807-c5dc6710d237")
        => new(
            "lan_1a2b3c4d",
            type,
            amount,
            currency,
            DateTimeOffset.Parse("2026-02-16T12:00:00Z", CultureInfo.InvariantCulture),
            merchantId,
            occurredAt ?? DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture),
            "Venda aprovada",
            correlationId,
            "order-123");
}
