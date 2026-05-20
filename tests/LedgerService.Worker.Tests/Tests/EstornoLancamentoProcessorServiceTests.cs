using FluentAssertions;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Estornos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.Worker.Tests.Tests;

public sealed class EstornoLancamentoProcessorServiceTests
{
    [Fact]
    public async Task ProcessOnce_should_delegate_pending_estornos_to_mediator()
    {
        var pending = new EstornoLancamento(Guid.NewGuid(), "m1", "Erro operacional", Guid.NewGuid());
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var sender = new Mock<ISender>(MockBehavior.Strict);

        repo.Setup(x => x.ClaimPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);
        sender.Setup(x => x.Send(
                It.Is<ProcessarEstornoLancamentoCommand>(cmd => cmd.EstornoId == pending.Id),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var sut = CreateSut(repo.Object, sender.Object);

        await sut.ProcessOnceAsync(CancellationToken.None);

        repo.VerifyAll();
        sender.VerifyAll();
    }

    [Fact]
    public async Task ProcessOnce_should_not_delegate_completed_estornos_returned_by_repository_filter()
    {
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var sender = new Mock<ISender>(MockBehavior.Strict);

        repo.Setup(x => x.ClaimPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var sut = CreateSut(repo.Object, sender.Object);

        await sut.ProcessOnceAsync(CancellationToken.None);

        sender.Verify(x => x.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.VerifyAll();
    }

    [Fact]
    public async Task ProcessOnce_should_respect_cancellation()
    {
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var sender = new Mock<ISender>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        repo.Setup(x => x.ClaimPendingAsync(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        using var sut = CreateSut(repo.Object, sender.Object);

        var act = async () => await sut.ProcessOnceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static EstornoLancamentoProcessorService CreateSut(
        IEstornoLancamentoRepository repo,
        ISender sender)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repo);
        services.AddSingleton(sender);

        return new EstornoLancamentoProcessorService(
            services.BuildServiceProvider(),
            Options.Create(new EstornoProcessingOptions { PollingIntervalSeconds = 1, BatchSize = 10 }),
            NullLogger<EstornoLancamentoProcessorService>.Instance);
    }
}
