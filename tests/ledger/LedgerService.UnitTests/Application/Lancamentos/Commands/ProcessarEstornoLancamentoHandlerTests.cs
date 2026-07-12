using System.Text.Json;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Policies;
using LedgerService.Domain.Repositories;

using Microsoft.Extensions.Logging.Abstractions;

namespace LedgerService.UnitTests.Application.Lancamentos.Commands;

public sealed class ProcessarEstornoLancamentoHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_process_pending_estorno_create_compensating_entry_and_outbox_event()
    {
        var original = NewLedgerEntry(LedgerEntryType.Credit, 100m);
        var estorno = new EstornoLancamento(original.Id, original.MerchantId, "Erro operacional", original.CorrelationId, DateTime.UtcNow);
        var state = new State([original], [estorno]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarEstornoLancamentoCommand(estorno.Id), CancellationToken.None);
        Assert.Equal(EstornoLancamentoStatus.Completed, estorno.Status);
        Assert.NotNull(estorno.ProcessingStartedAt);
        Assert.NotNull(estorno.CompletedAt);
        Assert.NotNull(estorno.LancamentoCompensatorioId);
        var compensating = state.LedgerEntries.Single(x => x.Id == estorno.LancamentoCompensatorioId);
        Assert.Equal(LedgerEntryType.Debit, compensating.Type);
        Assert.Equal(-100m, compensating.Amount);
        Assert.Equal(original.MerchantId, compensating.MerchantId);
        Assert.Equal($"estorno:{original.Id:N}", compensating.ExternalReference);
        Assert.Single(state.OutboxMessages);
        var outbox = state.OutboxMessages.Single();
        Assert.Equal(LedgerEntryCreatedV2.EventType, outbox.EventType);
        Assert.Equal(compensating.Id, outbox.AggregateId);
        var evt = JsonSerializer.Deserialize<LedgerEntryCreatedV2>(outbox.Payload, JsonOptions);
        Assert.NotNull(evt);
        Assert.Equal("DEBIT", evt.Type);
        Assert.Equal("-100.00", evt.Amount);
        Assert.Equal("BRL", evt.Currency);
        Assert.Equal(original.MerchantId, evt.MerchantId);
    }

    [Fact]
    public async Task Should_reject_when_original_lancamento_does_not_exist()
    {
        var estorno = new EstornoLancamento(Guid.NewGuid(), "m1", "Erro operacional", Guid.NewGuid(), DateTime.UtcNow);
        var state = new State([], [estorno]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarEstornoLancamentoCommand(estorno.Id), CancellationToken.None);
        Assert.Equal(EstornoLancamentoStatus.Rejected, estorno.Status);
        Assert.Contains("Lancamento original", estorno.RejectionReason);
        Assert.Empty(state.LedgerEntries);
        Assert.Empty(state.OutboxMessages);
    }

    [Fact]
    public async Task Should_not_duplicate_compensating_entry_or_outbox_when_processed_again()
    {
        var original = NewLedgerEntry(LedgerEntryType.Debit, -50m);
        var estorno = new EstornoLancamento(original.Id, original.MerchantId, "Erro operacional", original.CorrelationId, DateTime.UtcNow);
        var state = new State([original], [estorno]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarEstornoLancamentoCommand(estorno.Id), CancellationToken.None);
        await sut.Handle(new ProcessarEstornoLancamentoCommand(estorno.Id), CancellationToken.None);

        var compensatingEntry = Assert.Single(state.LedgerEntries, x => x.ExternalReference == $"estorno:{original.Id:N}");
        Assert.Equal(50m, compensatingEntry.Amount);
        Assert.Single(state.OutboxMessages);
        Assert.Equal(EstornoLancamentoStatus.Completed, estorno.Status);
    }

    [Fact]
    public async Task Should_reject_when_lancamento_already_has_completed_estorno()
    {
        var original = NewLedgerEntry(LedgerEntryType.Credit, 25m);
        var completed = new EstornoLancamento(original.Id, original.MerchantId, "Primeiro", original.CorrelationId, DateTime.UtcNow);
        completed.MarkProcessing(DateTime.UtcNow);
        completed.Complete(Guid.NewGuid(), DateTime.UtcNow);
        var pending = new EstornoLancamento(original.Id, original.MerchantId, "Segundo", original.CorrelationId, DateTime.UtcNow);
        var state = new State([original], [completed, pending]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarEstornoLancamentoCommand(pending.Id), CancellationToken.None);
        Assert.Equal(EstornoLancamentoStatus.Rejected, pending.Status);
        Assert.Contains("ja foi estornado", pending.RejectionReason);
        Assert.Single(state.LedgerEntries, x => x.Id == original.Id);
        Assert.Empty(state.OutboxMessages);
    }

    private static ProcessarEstornoLancamentoHandler CreateSut(State state)
        => new(
            new ProcessarEstornoLancamentoDependencies(
                new EstornoRepo(state),
                new LedgerRepo(state),
                new OutboxRepo(state),
                new UnitOfWork()),
            new LedgerReversalPolicy(new EstornoRepo(state), new LedgerRepo(state)),
            NullLogger<ProcessarEstornoLancamentoHandler>.Instance);

    private static LedgerEntry NewLedgerEntry(LedgerEntryType type, decimal amount)
        => new("m1", type, amount, DateTime.UtcNow, "Venda", "ext", Guid.NewGuid(), DateTime.UtcNow);

    private sealed record State(
        List<LedgerEntry> LedgerEntries,
        List<EstornoLancamento> Estornos,
        List<OutboxMessage> OutboxMessages)
    {
        public State(IEnumerable<LedgerEntry> ledgerEntries, IEnumerable<EstornoLancamento> estornos)
            : this([.. ledgerEntries], [.. estornos], [])
        {
        }
    }

    private sealed class LedgerRepo(State state) : ILedgerEntryRepository
    {
        private readonly State _state = state;

        public Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.LedgerEntries.FirstOrDefault(x => x.Id == id));

        public Task<LedgerEntry?> GetCompensatingEntryAsync(Guid lancamentoOriginalId, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.LedgerEntries.FirstOrDefault(x => x.ExternalReference == $"estorno:{lancamentoOriginalId:N}"));

        public Task<IReadOnlyList<LedgerEntry>> ListByMerchantAndPeriodAsync(
            string merchantId,
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LedgerEntry>>(
                [.. _state.LedgerEntries.Where(x => x.MerchantId == merchantId && x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)]);

        public Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default)
        {
            _state.LedgerEntries.Add(ledgerEntry);
            return Task.CompletedTask;
        }
    }

    private sealed class EstornoRepo(State state) : IEstornoLancamentoRepository
    {
        private readonly State _state = state;

        public Task<EstornoLancamento?> GetByIdAsync(Guid estornoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.Estornos.FirstOrDefault(x => x.Id == estornoId));

        public Task<EstornoLancamento?> GetByIdForUpdateAsync(Guid estornoId, CancellationToken cancellationToken = default)
            => GetByIdAsync(estornoId, cancellationToken);

        public Task<IReadOnlyList<EstornoLancamento>> ClaimPendingAsync(int maxItems, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EstornoLancamento>>([.. _state.Estornos.Where(x => x.Status == EstornoLancamentoStatus.Pending).Take(maxItems)]);

        public Task<EstornoLancamento?> GetActiveByLancamentoOriginalIdAsync(Guid lancamentoOriginalId, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.Estornos.FirstOrDefault(x => x.LancamentoOriginalId == lancamentoOriginalId && x.IsActive()));

        public Task<EstornoLancamento?> GetCompletedByLancamentoOriginalIdAsync(Guid lancamentoOriginalId, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.Estornos.FirstOrDefault(x => x.LancamentoOriginalId == lancamentoOriginalId && x.Status == EstornoLancamentoStatus.Completed));

        public Task AddAsync(EstornoLancamento estorno, CancellationToken cancellationToken = default)
        {
            _state.Estornos.Add(estorno);
            return Task.CompletedTask;
        }
    }

    private sealed class OutboxRepo(State state) : IOutboxMessageRepository
    {
        private readonly State _state = state;

        public Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
        {
            _state.OutboxMessages.Add(outboxMessage);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, DateTime now, string lockOwner, TimeSpan lockDuration, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkProcessedAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OutboxStatus> MarkFailedPublishAttemptAsync(Guid id, int maxRetries, DateTime nextRetryAt, string? lastError, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<OutboxMessage> Items, int TotalCount)> GetDeadLettersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<OutboxMessage>> RequeueDeadLettersAsync(Guid? id, string? eventType, DateTime? occurredFrom, DateTime? occurredUntil, int limit, DateTime requeuedAt, string requeuedBy, string reason, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IAppTransaction>(new Transaction());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }

    private sealed class Transaction : IAppTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
