using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using BalanceService.UnitTests.Fixtures;
using FluentAssertions;

namespace BalanceService.UnitTests.Tests;

public sealed class DailyBalanceDomainTests
{
    [Fact]
    public void Ctor_should_validate_merchant_and_currency()
    {
        var act1 = () => new DailyBalance(" ", new DateOnly(2026, 2, 16), "BRL", DateTimeOffset.UtcNow);
        act1.Should().Throw<DomainException>().WithMessage("*MerchantId*");

        var act2 = () => new DailyBalance("m1", new DateOnly(2026, 2, 16), "BR", DateTimeOffset.UtcNow);
        act2.Should().Throw<DomainException>().WithMessage("*Currency*");
    }

    [Fact]
    public void Apply_should_add_credit_and_update_asof_and_net()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "brl", now);

        var evt = BalanceFixture.Event(type: "CREDIT", amount: "10.50", occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00"));
        balance.Apply(evt, now);

        balance.TotalCredits.Should().Be(10.50m);
        balance.TotalDebits.Should().Be(0m);
        balance.NetBalance.Should().Be(10.50m);
        balance.AsOf.Should().Be(evt.OccurredAt);
        balance.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Apply_should_add_debit_as_magnitude_and_net_should_decrease()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);

        // No Ledger, debit tende a ser negativo.
        var evt = BalanceFixture.Event(type: "DEBIT", amount: "-20.00");
        balance.Apply(evt, now);

        balance.TotalDebits.Should().Be(20.00m);
        balance.NetBalance.Should().Be(-20.00m);
    }

    [Theory]
    [InlineData("CREDIT", "0")]
    [InlineData("DEBIT", "0")]
    [InlineData("CREDIT", "abc")]
    public void Apply_should_reject_invalid_amount(string type, string amount)
    {
        var now = DateTimeOffset.UtcNow;
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);
        var evt = BalanceFixture.Event(type: type, amount: amount);

        var act = () => balance.Apply(evt, now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Apply_should_reject_invalid_type()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = new DailyBalance("m1", new DateOnly(2026, 2, 16), "BRL", now);
        var evt = BalanceFixture.Event(type: "X", amount: "10.00");

        var act = () => balance.Apply(evt, now);
        act.Should().Throw<DomainException>().WithMessage("*CREDIT or DEBIT*");
    }
}
