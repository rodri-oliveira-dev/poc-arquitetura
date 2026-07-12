using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Repositories;

namespace LedgerService.Domain.Policies;

public sealed class LedgerReversalPolicy
{
    private readonly IEstornoLancamentoRepository _estornoRepository;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;

    public LedgerReversalPolicy(
        IEstornoLancamentoRepository estornoRepository,
        ILedgerEntryRepository ledgerEntryRepository)
    {
        ArgumentNullException.ThrowIfNull(estornoRepository);
        ArgumentNullException.ThrowIfNull(ledgerEntryRepository);

        _estornoRepository = estornoRepository;
        _ledgerEntryRepository = ledgerEntryRepository;
    }

    public async Task EnsureCanRequestReversalAsync(
        LedgerEntry lancamentoOriginal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lancamentoOriginal);

        var activeEstorno = await _estornoRepository
            .GetActiveByLancamentoOriginalIdAsync(lancamentoOriginal.Id, cancellationToken);

        if (activeEstorno is not null)
            throw new DomainException("Lancamento ja possui solicitacao ativa de estorno.");

        var completedEstorno = await _estornoRepository
            .GetCompletedByLancamentoOriginalIdAsync(lancamentoOriginal.Id, cancellationToken);

        if (completedEstorno is not null)
            throw new DomainException("Lancamento ja foi estornado.");

        var compensatingEntry = await _ledgerEntryRepository
            .GetCompensatingEntryAsync(lancamentoOriginal.Id, cancellationToken);

        if (compensatingEntry is not null)
            throw new DomainException("Lancamento ja possui lancamento compensatorio.");
    }

    public async Task EnsureCanCompleteReversalAsync(
        EstornoLancamento estorno,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(estorno);

        var completedEstorno = await _estornoRepository.GetCompletedByLancamentoOriginalIdAsync(
            estorno.LancamentoOriginalId,
            cancellationToken);

        if (completedEstorno is not null && completedEstorno.Id != estorno.Id)
            throw new DomainException("Lancamento ja foi estornado.");
    }
}
