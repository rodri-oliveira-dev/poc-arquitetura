using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Commands;
using BalanceService.Application.Idempotency;
using BalanceService.Domain.Balances;
using BalanceService.UnitTests.Fixtures;

using Microsoft.Extensions.Logging;

using Moq;

namespace BalanceService.UnitTests.Application.Balances.Commands;

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
        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        ProcessedEvent? processedEvent = null;
        processedRepo.Setup(x => x.TryInsertAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedEvent, CancellationToken>((e, _) => processedEvent = e)
            .ReturnsAsync(false);

        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new ApplyLedgerEntryCreatedHandler(dailyRepo.Object, processedRepo.Object, uow.Object, clock.Object, logger.Object);

        await sut.Handle(new ApplyLedgerEntryCreatedCommand(evt), CancellationToken.None);

        // Não chama repo/SaveChanges quando já processado.
        Assert.NotNull(processedEvent);
        Assert.Equal(evt.Id, processedEvent.EventId);
        Assert.Equal(evt.MerchantId, processedEvent.MerchantId);
        Assert.Equal(evt.OccurredAt.ToUniversalTime(), processedEvent.OccurredAt);
        Assert.Equal(now, processedEvent.ProcessedAt);
        dailyRepo.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.DisposeAsync(), Times.Once);
        processedRepo.VerifyAll();
        uow.VerifyAll();
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

        var evt = BalanceFixture.Event(id: "e1", type: "CREDIT", amount: "10.00", occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture));
        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        ProcessedEvent? processedEvent = null;
        processedRepo.Setup(x => x.TryInsertAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedEvent, CancellationToken>((e, _) => processedEvent = e)
            .ReturnsAsync(true);

        var dailySequence = new MockSequence();
        dailyRepo.InSequence(dailySequence)
            .Setup(x => x.LockByMerchantDateAndCurrencyAsync(evt.MerchantId, It.IsAny<DateOnly>(), "BRL", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DateOnly? requestedDate = null;
        dailyRepo.InSequence(dailySequence)
            .Setup(x => x.GetByMerchantDateAndCurrencyAsync(evt.MerchantId, It.IsAny<DateOnly>(), "BRL", It.IsAny<CancellationToken>()))
            .Callback<string, DateOnly, string, CancellationToken>((_, date, _, _) => requestedDate = date)
            .ReturnsAsync((DailyBalance?)null);

        DailyBalance? created = null;
        dailyRepo.InSequence(dailySequence)
            .Setup(x => x.AddAsync(It.IsAny<DailyBalance>(), It.IsAny<CancellationToken>()))
            .Callback<DailyBalance, CancellationToken>((b, _) => created = b)
            .Returns(Task.CompletedTask);

        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new ApplyLedgerEntryCreatedHandler(dailyRepo.Object, processedRepo.Object, uow.Object, clock.Object, logger.Object);

        await sut.Handle(new ApplyLedgerEntryCreatedCommand(evt), CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal(new DateOnly(2026, 2, 16), requestedDate);
        Assert.NotNull(processedEvent);
        Assert.Equal(evt.Id, processedEvent.EventId);
        Assert.Equal(evt.MerchantId, processedEvent.MerchantId);
        Assert.Equal(evt.OccurredAt.ToUniversalTime(), processedEvent.OccurredAt);
        Assert.Equal(now, processedEvent.ProcessedAt);
        Assert.Equal(evt.MerchantId, created.MerchantId);
        Assert.Equal(new DateOnly(2026, 2, 16), created.Date);
        Assert.Equal("BRL", created.Currency);
        Assert.Equal(10m, created.TotalCredits);
        Assert.Equal(0m, created.TotalDebits);
        Assert.Equal(10m, created.NetBalance);
        Assert.Equal(evt.OccurredAt.ToUniversalTime(), created.AsOf);
        Assert.Equal(now, created.UpdatedAt);
        dailyRepo.VerifyAll();
        processedRepo.VerifyAll();
        uow.VerifyAll();
        tx.VerifyAll();
    }
}
