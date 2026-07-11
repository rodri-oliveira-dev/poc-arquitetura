using System.Globalization;

using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using BalanceService.UnitTests.Fixtures;

namespace BalanceService.UnitTests.Domain.Balances;

public sealed class DailyBalanceDomainTests
{
    [Fact]
    public void Ctor_should_create_daily_balance_with_valid_merchant()
    {
        var now = Instant("2026-02-16T03:00:00Z");

        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);

        Assert.Equal("m1", balance.MerchantId);
        Assert.Equal(new DateOnly(2026, 2, 16), balance.Date);
        Assert.Equal("BRL", balance.Currency);
        Assert.Equal(now, balance.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Ctor_should_reject_missing_merchant(string merchantId)
    {
        DailyBalance act() => new DailyBalance(merchantId, new DateOnly(2026, 2, 16), "BRL", DateTimeOffset.UtcNow);

        var ex = Assert.Throws<DomainException>((Func<DailyBalance>)act);
        Assert.Contains("MerchantId", ex.Message);
    }

    [Fact]
    public void Ctor_should_normalize_currency_to_uppercase()
    {
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), " brl ", DateTimeOffset.UtcNow);

        Assert.Equal("BRL", balance.Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BR")]
    [InlineData("BRL1")]
    public void Ctor_should_reject_invalid_currency(string currency)
    {
        DailyBalance act() => new DailyBalance("m1", new DateOnly(2026, 2, 16), currency, DateTimeOffset.UtcNow);

        var ex = Assert.Throws<DomainException>((Func<DailyBalance>)act);
        Assert.Contains("Currency", ex.Message);
    }

    [Fact]
    public void Apply_should_add_credit_and_update_asof_and_net()
    {
        var now = Instant("2026-02-16T03:00:00Z");
        var occurredAt = Instant("2026-02-16T13:00:00Z");
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "brl", now);

        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Credit, amount: 10.50m, occurredAt: occurredAt), now);

        Assert.Equal(10.50m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(10.50m, balance.NetBalance);
        Assert.Equal(occurredAt, balance.AsOf);
        Assert.Equal(now, balance.UpdatedAt);
    }

    [Theory]
    [InlineData(-20.00)]
    [InlineData(20.00)]
    public void Apply_should_add_debit_as_magnitude_and_net_should_decrease(decimal debitAmount)
    {
        var now = Instant("2026-02-16T03:00:00Z");
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);

        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Debit, amount: debitAmount), now);

        Assert.Equal(20.00m, balance.TotalDebits);
        Assert.Equal(-20.00m, balance.NetBalance);
    }

    [Fact]
    public void Apply_should_reject_zero_amount()
    {
        static BalanceMovement act() => BalanceFixture.Movement(amount: 0m);

        var ex = Assert.Throws<DomainException>((Func<BalanceMovement>)act);
        Assert.Contains("Amount", ex.Message);
    }

    [Fact]
    public void Apply_should_reject_invalid_movement_type()
    {
        var now = Instant("2026-02-16T03:00:00Z");
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);

        BalanceMovement act() => new BalanceMovement(
            "m1",
            new DateOnly(2026, 2, 16),
            new Currency("BRL"),
            (BalanceMovementType)999,
            new BalanceAmount(10m),
            now);

        var ex = Assert.Throws<DomainException>((Func<BalanceMovement>)act);
        Assert.Contains("Type", ex.Message);
        Assert.Equal(0m, balance.NetBalance);
    }

    [Fact]
    public void Apply_should_update_asof_only_when_movement_is_more_recent()
    {
        var now = Instant("2026-02-16T03:00:00Z");
        var newer = Instant("2026-02-16T13:00:00Z");
        var older = Instant("2026-02-16T12:00:00Z");
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);

        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Credit, amount: 100m, occurredAt: newer), now);
        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Debit, amount: -10m, occurredAt: older), now.AddMinutes(1));

        Assert.Equal(newer, balance.AsOf);
        Assert.Equal(now.AddMinutes(1), balance.UpdatedAt);
    }

    [Fact]
    public void Apply_should_accumulate_multiple_movements()
    {
        var now = Instant("2026-02-16T03:00:00Z");
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);

        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Credit, amount: 100m), now);
        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Debit, amount: -30m), now.AddMinutes(1));
        balance.Apply(BalanceFixture.Movement(type: BalanceMovementType.Credit, amount: 5m), now.AddMinutes(2));

        Assert.Equal(105m, balance.TotalCredits);
        Assert.Equal(30m, balance.TotalDebits);
        Assert.Equal(75m, balance.NetBalance);
    }

    private static DateTimeOffset Instant(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
