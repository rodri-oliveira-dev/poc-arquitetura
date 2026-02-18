using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.UnitTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class ApplyLedgerEntryCreatedHandlerTests
{
    [Fact]
    public async Task Should_do_nothing_when_event_already_processed()
    {
        var dailyRepo = new Mock<IDailyBalanceRepository>(MockBehavior.Strict);
        var processedRepo = new Mock<IProcessedEventRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var logger = new Mock<ILogger<ApplyLedgerEntryCreatedHandler>>();
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var evt = BalanceFixture.Event(id: "e1");
        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z");
        clock.SetupGet(x => x.UtcNow).Returns(now);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        processedRepo.Setup(x => x.TryInsertAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new ApplyLedgerEntryCreatedHandler(dailyRepo.Object, processedRepo.Object, uow.Object, clock.Object, logger.Object);

        await sut.HandleAsync(new ApplyLedgerEntryCreatedCommand(evt), CancellationToken.None);

        // Não chama repo/SaveChanges quando já processado.
        dailyRepo.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_create_daily_balance_when_missing_and_apply_event()
    {
        var dailyRepo = new Mock<IDailyBalanceRepository>(MockBehavior.Strict);
        var processedRepo = new Mock<IProcessedEventRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var logger = new Mock<ILogger<ApplyLedgerEntryCreatedHandler>>();
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var evt = BalanceFixture.Event(id: "e1", type: "CREDIT", amount: "10.00", occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00"));
        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z");
        clock.SetupGet(x => x.UtcNow).Returns(now);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        processedRepo.Setup(x => x.TryInsertAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        dailyRepo.Setup(x => x.GetByMerchantDateAndCurrencyAsync(evt.MerchantId, It.IsAny<DateOnly>(), "BRL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyBalance?)null);

        DailyBalance? created = null;
        dailyRepo.Setup(x => x.AddAsync(It.IsAny<DailyBalance>(), It.IsAny<CancellationToken>()))
            .Callback<DailyBalance, CancellationToken>((b, _) => created = b)
            .Returns(Task.CompletedTask);

        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new ApplyLedgerEntryCreatedHandler(dailyRepo.Object, processedRepo.Object, uow.Object, clock.Object, logger.Object);

        await sut.HandleAsync(new ApplyLedgerEntryCreatedCommand(evt), CancellationToken.None);

        created.Should().NotBeNull();
        created!.MerchantId.Should().Be(evt.MerchantId);
        created!.Currency.Should().Be("BRL");
        created!.TotalCredits.Should().Be(10m);
    }
}
