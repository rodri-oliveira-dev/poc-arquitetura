using System.Globalization;

using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using FluentAssertions;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class GetPeriodBalanceHandlerTests
{
    [Fact]
    public async Task Handle_should_delegate_to_period_balance_service()
    {
        var service = new Mock<IPeriodBalanceService>(MockBehavior.Strict);
        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));
        var expected = new PeriodBalanceReadModel(
            MerchantId: query.MerchantId,
            From: query.From,
            To: query.To,
            Currency: "BRL",
            TotalCredits: 10m,
            TotalDebits: 3m,
            NetBalance: 7m,
            Items: Array.Empty<DailyBalanceReadModel>(),
            CalculatedAt: DateTimeOffset.Parse("2026-02-12T10:00:00Z", CultureInfo.InvariantCulture));

        service
            .Setup(x => x.GetPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new GetPeriodBalanceHandler(service.Object);

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().Be(expected);
        service.VerifyAll();
    }
}
