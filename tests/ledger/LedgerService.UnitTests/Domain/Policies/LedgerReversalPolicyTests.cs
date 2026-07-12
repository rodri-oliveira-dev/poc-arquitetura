using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Policies;
using LedgerService.Domain.Repositories;

namespace LedgerService.UnitTests.Domain.Policies;

public sealed class LedgerReversalPolicyTests
{
    [Fact]
    public async Task Should_allow_first_reversal()
    {
        var original = NewLedgerEntry();
        var estornoRepo = new EstornoRepo([]);
        var ledgerRepo = new LedgerRepo([original]);
        var policy = new LedgerReversalPolicy(estornoRepo, ledgerRepo);

        await policy.EnsureCanRequestReversalAsync(original, TestContext.Current.CancellationToken);

        Assert.Equal(1, estornoRepo.ActiveLookupCount);
        Assert.Equal(1, estornoRepo.CompletedLookupCount);
        Assert.Equal(1, ledgerRepo.CompensatingLookupCount);
        Assert.Equal("ext", original.ExternalReference);
    }

    [Fact]
    public async Task Should_reject_when_active_reversal_exists()
    {
        var original = NewLedgerEntry();
        var active = new EstornoLancamento(
            original.Id,
            original.MerchantId,
            "Erro operacional",
            Guid.NewGuid(),
            DateTime.UtcNow);
        var policy = new LedgerReversalPolicy(
            new EstornoRepo([active]),
            new LedgerRepo([original]));

        async Task Act()
        {
            await policy.EnsureCanRequestReversalAsync(
                original,
                TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<DomainException>(Act);
        Assert.Contains("solicitacao ativa", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_reject_when_completed_reversal_exists()
    {
        var original = NewLedgerEntry();
        var completed = new EstornoLancamento(
            original.Id,
            original.MerchantId,
            "Erro operacional",
            Guid.NewGuid(),
            DateTime.UtcNow);
        completed.MarkProcessing(DateTime.UtcNow);
        completed.Complete(Guid.NewGuid(), DateTime.UtcNow);
        var policy = new LedgerReversalPolicy(
            new EstornoRepo([completed]),
            new LedgerRepo([original]));

        async Task Act()
        {
            await policy.EnsureCanRequestReversalAsync(
                original,
                TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<DomainException>(Act);
        Assert.Contains("ja foi estornado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_reject_when_compensating_entry_already_exists()
    {
        var original = NewLedgerEntry();
        var compensating = original.CriarLancamentoCompensatorio(
            Guid.NewGuid(),
            "Erro operacional",
            DateTime.UtcNow);
        var policy = new LedgerReversalPolicy(
            new EstornoRepo([]),
            new LedgerRepo([original, compensating]));

        async Task Act()
        {
            await policy.EnsureCanRequestReversalAsync(
                original,
                TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<DomainException>(Act);
        Assert.Contains("lancamento compensatorio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LedgerEntry NewLedgerEntry()
        => LedgerEntry.RegistrarCredito(
            "m1",
            100m,
            DateTime.UtcNow,
            "Venda",
            "ext",
            Guid.NewGuid(),
            DateTime.UtcNow);

    private sealed class EstornoRepo(IReadOnlyList<EstornoLancamento> estornos) : IEstornoLancamentoRepository
    {
        private readonly IReadOnlyList<EstornoLancamento> _estornos = estornos;

        public Task<EstornoLancamento?> GetByIdAsync(Guid estornoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_estornos.FirstOrDefault(x => x.Id == estornoId));

        public Task<EstornoLancamento?> GetByIdForUpdateAsync(Guid estornoId, CancellationToken cancellationToken = default)
            => GetByIdAsync(estornoId, cancellationToken);

        public Task<IReadOnlyList<EstornoLancamento>> ClaimPendingAsync(
            int maxItems,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EstornoLancamento>>(
                [.. _estornos.Where(x => x.Status == EstornoLancamentoStatus.Pending).Take(maxItems)]);

        public Task<EstornoLancamento?> GetActiveByLancamentoOriginalIdAsync(
            Guid lancamentoOriginalId,
            CancellationToken cancellationToken = default)
        {
            ActiveLookupCount++;
            return Task.FromResult(
                _estornos.FirstOrDefault(x => x.LancamentoOriginalId == lancamentoOriginalId && x.IsActive()));
        }

        public Task<EstornoLancamento?> GetCompletedByLancamentoOriginalIdAsync(
            Guid lancamentoOriginalId,
            CancellationToken cancellationToken = default)
        {
            CompletedLookupCount++;
            return Task.FromResult(
                _estornos.FirstOrDefault(x =>
                    x.LancamentoOriginalId == lancamentoOriginalId &&
                    x.Status == EstornoLancamentoStatus.Completed));
        }

        public Task AddAsync(EstornoLancamento estorno, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public int ActiveLookupCount
        {
            get; private set;
        }

        public int CompletedLookupCount
        {
            get; private set;
        }
    }

    private sealed class LedgerRepo(IReadOnlyList<LedgerEntry> entries) : ILedgerEntryRepository
    {
        private readonly IReadOnlyList<LedgerEntry> _entries = entries;

        public Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(x => x.Id == id));

        public Task<LedgerEntry?> GetCompensatingEntryAsync(
            Guid lancamentoOriginalId,
            CancellationToken cancellationToken = default)
        {
            CompensatingLookupCount++;
            return Task.FromResult(
                _entries.FirstOrDefault(x => x.ExternalReference == $"estorno:{lancamentoOriginalId:N}"));
        }

        public Task<IReadOnlyList<LedgerEntry>> ListByMerchantAndPeriodAsync(
            string merchantId,
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LedgerEntry>>(
                [.. _entries
                    .Where(x =>
                        x.MerchantId == merchantId &&
                        x.OccurredAt >= startInclusive &&
                        x.OccurredAt < endExclusive)]);

        public Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public int CompensatingLookupCount
        {
            get; private set;
        }
    }
}
