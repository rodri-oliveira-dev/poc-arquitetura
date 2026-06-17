using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Common.Exceptions;
using TransferService.Application.Transferencias.Commands;
using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;

namespace TransferService.UnitTests.Application.Transferencias.Commands;

public sealed class SolicitarTransferenciaCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_should_create_valid_saga()
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.TransferenciaId);
        Assert.Equal(TransferenciaSagaStatus.Pending.ToString(), result.Status);
        Assert.Equal("merchant-source", result.SourceMerchantId);
        Assert.Equal("merchant-destination", result.DestinationMerchantId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(Now, result.CreatedAt);
        Assert.False(result.IdempotentReplay);

        var saga = Assert.Single(fixture.SagaRepository.Sagas);
        Assert.Equal(result.TransferenciaId, saga.Id);
        Assert.Equal(TransferenciaSagaStatus.Pending, saga.Status);
    }

    [Fact]
    public async Task Handle_should_write_transferencia_solicitada_event_to_outbox_port()
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(ValidCommand(), CancellationToken.None);

        var evento = Assert.IsType<TransferenciaSolicitadaV1>(Assert.Single(fixture.OutboxWriter.Events));
        Assert.Equal(TransferenciaSolicitadaV1.Type, evento.EventType);
        Assert.Equal(result.TransferenciaId, evento.TransferenciaId);
        Assert.Equal("merchant-source", evento.SourceMerchantId);
        Assert.Equal("merchant-destination", evento.DestinationMerchantId);
        Assert.Equal(100m, evento.Amount);
        Assert.Equal(Now, evento.OccurredAt);
        Assert.Equal("correlation-1", evento.CorrelationId);
    }

    [Fact]
    public async Task Handle_should_return_idempotent_replay_for_same_key_and_payload()
    {
        var fixture = new HandlerFixture();
        var command = ValidCommand();
        var created = await fixture.Handler.Handle(command, CancellationToken.None);

        fixture.SagaRepository.Sagas.Clear();
        fixture.OutboxWriter.Events.Clear();

        var replay = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal(created.TransferenciaId, replay.TransferenciaId);
        Assert.Equal(created.Status, replay.Status);
        Assert.True(replay.IdempotentReplay);
        Assert.Empty(fixture.SagaRepository.Sagas);
        Assert.Empty(fixture.OutboxWriter.Events);
    }

    [Fact]
    public async Task Handle_should_reject_same_key_with_different_payload()
    {
        var fixture = new HandlerFixture();
        var command = ValidCommand();
        await fixture.Handler.Handle(command, CancellationToken.None);

        var differentPayload = command with { Amount = 150m };

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Handler.Handle(differentPayload, CancellationToken.None));

        Assert.Contains("different payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_should_reject_same_merchants()
    {
        var fixture = new HandlerFixture();
        var command = ValidCommand() with
        {
            SourceMerchantId = "same",
            DestinationMerchantId = "same"
        };

        var exception = await Assert.ThrowsAsync<TransferService.Domain.Exceptions.DomainException>(
            () => fixture.Handler.Handle(command, CancellationToken.None));

        Assert.Contains("nao pode ser igual", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_should_reject_invalid_amount()
    {
        var fixture = new HandlerFixture();
        var command = ValidCommand() with { Amount = 0m };

        var exception = await Assert.ThrowsAsync<TransferService.Domain.Exceptions.DomainException>(
            () => fixture.Handler.Handle(command, CancellationToken.None));

        Assert.Contains("maior que zero", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SolicitarTransferenciaCommand ValidCommand()
        => new(
            "idem-1",
            "merchant-source",
            "merchant-destination",
            100m,
            "correlation-1");

    private sealed class HandlerFixture
    {
        public HandlerFixture()
        {
            Handler = new SolicitarTransferenciaCommandHandler(
                SagaRepository,
                IdempotencyService,
                OutboxWriter,
                UnitOfWork,
                new FixedClock(Now));
        }

        public FakeTransferenciaSagaRepository SagaRepository { get; } = new();
        public FakeTransferenciaIdempotencyService IdempotencyService { get; } = new();
        public FakeTransferenciaOutboxWriter OutboxWriter { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public SolicitarTransferenciaCommandHandler Handler { get; }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeTransferenciaSagaRepository : ITransferenciaSagaRepository
    {
        public List<TransferenciaSaga> Sagas { get; } = [];

        public Task<TransferenciaSaga?> GetByIdAsync(Guid transferenciaId, CancellationToken cancellationToken)
            => Task.FromResult(Sagas.SingleOrDefault(saga => saga.Id == transferenciaId));

        public Task AddAsync(TransferenciaSaga saga, CancellationToken cancellationToken)
        {
            Sagas.Add(saga);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TransferenciaSaga>> ClaimPendingAsync(
            int batchSize,
            DateTimeOffset now,
            string lockOwner,
            TimeSpan lockDuration,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TransferenciaSaga>>([]);
    }

    private sealed class FakeTransferenciaIdempotencyService : ITransferenciaIdempotencyService
    {
        private readonly Dictionary<string, TransferenciaIdempotencyEntry> _entries = [];

        public Task<TransferenciaIdempotencyEntry?> GetAsync(
            string sourceMerchantId,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            _entries.TryGetValue(Key(sourceMerchantId, idempotencyKey), out var entry);
            return Task.FromResult(entry);
        }

        public Task AddAsync(
            string sourceMerchantId,
            string idempotencyKey,
            string requestHash,
            SolicitarTransferenciaResult response,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            _entries.Add(Key(sourceMerchantId, idempotencyKey), new TransferenciaIdempotencyEntry(requestHash, response));
            return Task.CompletedTask;
        }

        private static string Key(string sourceMerchantId, string idempotencyKey)
            => $"{sourceMerchantId}:{idempotencyKey}";
    }

    private sealed class FakeTransferenciaOutboxWriter : ITransferenciaOutboxWriter
    {
        public List<TransferenciaSagaEvent> Events { get; } = [];

        public Task WriteAsync(TransferenciaSagaEvent evento, CancellationToken cancellationToken)
        {
            Events.Add(evento);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IAppTransaction>(new FakeAppTransaction());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            => Task.FromResult(1);
    }

    private sealed class FakeAppTransaction : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
