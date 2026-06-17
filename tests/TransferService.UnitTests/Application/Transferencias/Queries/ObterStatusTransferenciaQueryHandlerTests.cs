using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Common.Exceptions;
using TransferService.Application.Transferencias.Queries;
using TransferService.Domain.Sagas;

namespace TransferService.UnitTests.Application.Transferencias.Queries;

public sealed class ObterStatusTransferenciaQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_should_return_existing_status()
    {
        var repository = new FakeTransferenciaSagaRepository();
        var saga = CreateSaga();
        await repository.AddAsync(saga, CancellationToken.None);
        var handler = new ObterStatusTransferenciaQueryHandler(repository);

        var result = await handler.Handle(
            new ObterStatusTransferenciaQuery(saga.Id, ["merchant-source"]),
            CancellationToken.None);

        Assert.Equal(saga.Id, result.TransferenciaId);
        Assert.Equal(TransferenciaSagaStatus.Pending.ToString(), result.Status);
        Assert.Equal("merchant-source", result.SourceMerchantId);
        Assert.Equal("merchant-destination", result.DestinationMerchantId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(Now, result.UpdatedAt);
    }

    [Fact]
    public async Task Handle_should_return_not_found_when_missing()
    {
        var handler = new ObterStatusTransferenciaQueryHandler(new FakeTransferenciaSagaRepository());

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new ObterStatusTransferenciaQuery(Guid.NewGuid(), ["merchant-source"]),
                CancellationToken.None));

        Assert.Contains("nao encontrada", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_should_respect_merchant_authorization()
    {
        var repository = new FakeTransferenciaSagaRepository();
        var saga = CreateSaga();
        await repository.AddAsync(saga, CancellationToken.None);
        var handler = new ObterStatusTransferenciaQueryHandler(repository);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(
                new ObterStatusTransferenciaQuery(saga.Id, ["other-merchant"]),
                CancellationToken.None));

        Assert.Contains("sem autorizacao", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TransferenciaSaga CreateSaga()
        => new(
            new MerchantId("merchant-source"),
            new MerchantId("merchant-destination"),
            new TransferAmount(100m),
            Now);

    private sealed class FakeTransferenciaSagaRepository : ITransferenciaSagaRepository
    {
        private readonly Dictionary<Guid, TransferenciaSaga> _sagas = [];

        public Task<TransferenciaSaga?> GetByIdAsync(Guid transferenciaId, CancellationToken cancellationToken)
        {
            _sagas.TryGetValue(transferenciaId, out var saga);
            return Task.FromResult(saga);
        }

        public Task AddAsync(TransferenciaSaga saga, CancellationToken cancellationToken)
        {
            _sagas.Add(saga.Id, saga);
            return Task.CompletedTask;
        }
    }
}
