using System.Text.Json;

using FluentAssertions;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace LedgerService.UnitTests.Application.Lancamentos.Commands;

public sealed class ProcessarReprocessamentoLancamentosHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_process_pending_reprocessamento_and_republish_ledger_events()
    {
        var inPeriod = NewLedgerEntry("m1", new DateTime(2026, 5, 2, 10, 0, 0), LedgerEntryType.Credit, 100m);
        var otherMerchant = NewLedgerEntry("m2", new DateTime(2026, 5, 2, 10, 0, 0), LedgerEntryType.Credit, 75m);
        var outsidePeriod = NewLedgerEntry("m1", new DateTime(2026, 5, 8, 10, 0, 0), LedgerEntryType.Debit, -20m);
        var reprocessamento = NewReprocessamento();
        var state = new State([inPeriod, otherMerchant, outsidePeriod], [reprocessamento]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarReprocessamentoLancamentosCommand(reprocessamento.Id), CancellationToken.None);

        reprocessamento.Status.Should().Be(ReprocessamentoLancamentosStatus.Completed);
        reprocessamento.ProcessingStartedAt.Should().NotBeNull();
        reprocessamento.CompletedAt.Should().NotBeNull();
        state.OutboxMessages.Should().ContainSingle();

        var outbox = state.OutboxMessages.Single();
        outbox.AggregateType.Should().Be("LedgerEntryReprocessamento");
        outbox.AggregateId.Should().Be(inPeriod.Id);
        outbox.EventType.Should().Be(LedgerEntryCreatedV1.EventType);

        var evt = JsonSerializer.Deserialize<LedgerEntryCreatedV1>(outbox.Payload, JsonOptions);
        evt.Should().NotBeNull();
        evt!.Id.Should().Be($"lan_{inPeriod.Id.ToString("N")[..8]}");
        evt.Amount.Should().Be("100.00");
        evt.MerchantId.Should().Be("m1");
    }

    [Fact]
    public async Task Should_complete_with_warnings_when_no_entries_match()
    {
        var reprocessamento = NewReprocessamento();
        var state = new State([], [reprocessamento]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarReprocessamentoLancamentosCommand(reprocessamento.Id), CancellationToken.None);

        reprocessamento.Status.Should().Be(ReprocessamentoLancamentosStatus.CompletedWithWarnings);
        reprocessamento.FailureReason.Should().Contain("Nenhum lancamento");
        state.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_not_reprocess_completed_job_again()
    {
        var entry = NewLedgerEntry("m1", new DateTime(2026, 5, 2, 10, 0, 0), LedgerEntryType.Credit, 100m);
        var reprocessamento = NewReprocessamento();
        var state = new State([entry], [reprocessamento]);
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarReprocessamentoLancamentosCommand(reprocessamento.Id), CancellationToken.None);
        await sut.Handle(new ProcessarReprocessamentoLancamentosCommand(reprocessamento.Id), CancellationToken.None);

        reprocessamento.Status.Should().Be(ReprocessamentoLancamentosStatus.Completed);
        state.OutboxMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_mark_failed_on_technical_error()
    {
        var reprocessamento = NewReprocessamento();
        var state = new State([], [reprocessamento]) { ThrowWhenListingEntries = true };
        var sut = CreateSut(state);

        await sut.Handle(new ProcessarReprocessamentoLancamentosCommand(reprocessamento.Id), CancellationToken.None);

        reprocessamento.Status.Should().Be(ReprocessamentoLancamentosStatus.Failed);
        reprocessamento.FailureReason.Should().Contain("Falha tecnica");
    }

    private static ProcessarReprocessamentoLancamentosHandler CreateSut(State state)
        => new(
            new ReprocessamentoRepo(state),
            new LedgerRepo(state),
            new OutboxRepo(state),
            new UnitOfWork(),
            NullLogger<ProcessarReprocessamentoLancamentosHandler>.Instance);

    private static ReprocessamentoLancamentos NewReprocessamento()
        => new(
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            Guid.NewGuid());

    private static LedgerEntry NewLedgerEntry(
        string merchantId,
        DateTime occurredAt,
        LedgerEntryType type,
        decimal amount)
        => new(merchantId, type, amount, occurredAt, "Venda", null, Guid.NewGuid());

    private sealed record State(
        List<LedgerEntry> LedgerEntries,
        List<ReprocessamentoLancamentos> Reprocessamentos,
        List<OutboxMessage> OutboxMessages)
    {
        public State(IEnumerable<LedgerEntry> ledgerEntries, IEnumerable<ReprocessamentoLancamentos> reprocessamentos)
            : this(ledgerEntries.ToList(), reprocessamentos.ToList(), [])
        {
        }

        public bool ThrowWhenListingEntries { get; init; }
    }

    private sealed class ReprocessamentoRepo : IReprocessamentoLancamentosRepository
    {
        private readonly State _state;

        public ReprocessamentoRepo(State state) => _state = state;

        public Task<ReprocessamentoLancamentos?> GetByIdAsync(Guid reprocessamentoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.Reprocessamentos.FirstOrDefault(x => x.Id == reprocessamentoId));

        public Task<ReprocessamentoLancamentos?> GetByIdForUpdateAsync(Guid reprocessamentoId, CancellationToken cancellationToken = default)
            => GetByIdAsync(reprocessamentoId, cancellationToken);

        public Task AddAsync(ReprocessamentoLancamentos reprocessamento, CancellationToken cancellationToken = default)
        {
            _state.Reprocessamentos.Add(reprocessamento);
            return Task.CompletedTask;
        }
    }

    private sealed class LedgerRepo : ILedgerEntryRepository
    {
        private readonly State _state;

        public LedgerRepo(State state) => _state = state;

        public Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.LedgerEntries.FirstOrDefault(x => x.Id == id));

        public Task<LedgerEntry?> GetCompensatingEntryAsync(Guid lancamentoOriginalId, CancellationToken cancellationToken = default)
            => Task.FromResult(_state.LedgerEntries.FirstOrDefault(x => x.ExternalReference == $"estorno:{lancamentoOriginalId:N}"));

        public Task<IReadOnlyList<LedgerEntry>> ListByMerchantAndPeriodAsync(
            string merchantId,
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken = default)
        {
            if (_state.ThrowWhenListingEntries)
                throw new TimeoutException("DB timeout");

            return Task.FromResult<IReadOnlyList<LedgerEntry>>(
                _state.LedgerEntries
                    .Where(x => x.MerchantId == merchantId && x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)
                    .OrderBy(x => x.OccurredAt)
                    .ToList());
        }

        public Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default)
        {
            _state.LedgerEntries.Add(ledgerEntry);
            return Task.CompletedTask;
        }
    }

    private sealed class OutboxRepo : IOutboxMessageRepository
    {
        private readonly State _state;

        public OutboxRepo(State state) => _state = state;

        public Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
        {
            _state.OutboxMessages.Add(outboxMessage);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, DateTime now, string lockOwner, TimeSpan lockDuration, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkSentAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkFailedAttemptAsync(Guid id, int maxAttempts, DateTime nextAttemptAt, string? lastError, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<OutboxMessage>> RequeueFailedAsync(Guid? id, string? eventType, DateTime? occurredFrom, DateTime? occurredUntil, int limit, DateTime requeuedAt, string requeuedBy, string reason, CancellationToken cancellationToken = default)
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
