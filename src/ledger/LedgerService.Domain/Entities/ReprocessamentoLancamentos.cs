using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class ReprocessamentoLancamentos : Entity, IAggregateRoot
{
    private static readonly ReprocessamentoLancamentosStatus[] FinalStatuses =
    [
        ReprocessamentoLancamentosStatus.Completed,
        ReprocessamentoLancamentosStatus.CompletedWithWarnings,
        ReprocessamentoLancamentosStatus.Failed,
        ReprocessamentoLancamentosStatus.Rejected,
        ReprocessamentoLancamentosStatus.Canceled
    ];

    public string MerchantId
    {
        get; private set;
    }
    public DateOnly DataInicial
    {
        get; private set;
    }
    public DateOnly DataFinal
    {
        get; private set;
    }
    public string Motivo
    {
        get; private set;
    }
    public ReprocessamentoLancamentosStatus Status
    {
        get; private set;
    }
    public Guid CorrelationId
    {
        get; private set;
    }
    public DateTime CreatedAt
    {
        get; private set;
    }
    public DateTime? ProcessingStartedAt
    {
        get; private set;
    }
    public DateTime? CompletedAt
    {
        get; private set;
    }
    public DateTime? FailedAt
    {
        get; private set;
    }
    public DateTime? RejectedAt
    {
        get; private set;
    }
    public string? FailureReason
    {
        get; private set;
    }
    public string? RejectionReason
    {
        get; private set;
    }

    private ReprocessamentoLancamentos()
    {
        MerchantId = string.Empty;
        Motivo = string.Empty;
    }

    public ReprocessamentoLancamentos(
        string merchantId,
        DateOnly dataInicial,
        DateOnly dataFinal,
        string motivo,
        Guid correlationId,
        DateTime createdAt)
    {
        if (dataFinal < dataInicial)
            throw new DomainException("DataFinal nao pode ser menor que DataInicial.");

        MerchantId = string.IsNullOrWhiteSpace(merchantId)
            ? throw new DomainException("MerchantId e obrigatorio.")
            : merchantId.Trim();
        DataInicial = dataInicial;
        DataFinal = dataFinal;
        Motivo = string.IsNullOrWhiteSpace(motivo)
            ? throw new DomainException("Motivo e obrigatorio.")
            : motivo.Trim();
        Status = ReprocessamentoLancamentosStatus.Pending;
        CorrelationId = correlationId;
        CreatedAt = createdAt;
    }

    public bool IsFinal()
        => FinalStatuses.Contains(Status);

    public void MarkProcessing(DateTime now)
    {
        if (Status == ReprocessamentoLancamentosStatus.Processing)
        {
            ProcessingStartedAt ??= now;
            return;
        }

        EnsureCanTransitionTo(ReprocessamentoLancamentosStatus.Processing);

        Status = ReprocessamentoLancamentosStatus.Processing;
        ProcessingStartedAt ??= now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void Complete(int processedRecordsCount, DateTime now)
    {
        if (processedRecordsCount < 0)
            throw new DomainException("Quantidade de lancamentos reprocessados nao pode ser negativa.");

        if (processedRecordsCount == 0)
        {
            CompleteWithWarnings("Nenhum lancamento encontrado para o criterio informado.", now);
            return;
        }

        Complete(now);
    }

    public void Complete(DateTime now)
    {
        EnsureCanTransitionTo(ReprocessamentoLancamentosStatus.Completed);

        Status = ReprocessamentoLancamentosStatus.Completed;
        CompletedAt = now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void CompleteWithWarnings(string reason, DateTime now)
    {
        EnsureCanTransitionTo(ReprocessamentoLancamentosStatus.CompletedWithWarnings);

        Status = ReprocessamentoLancamentosStatus.CompletedWithWarnings;
        CompletedAt = now;
        FailureReason = NormalizeReason(reason);
        RejectionReason = null;
    }

    public void Reject(string reason, DateTime now)
    {
        EnsureCanTransitionTo(ReprocessamentoLancamentosStatus.Rejected);

        Status = ReprocessamentoLancamentosStatus.Rejected;
        RejectionReason = NormalizeReason(reason);
        RejectedAt = now;
    }

    public void Fail(string reason, DateTime now)
    {
        EnsureCanTransitionTo(ReprocessamentoLancamentosStatus.Failed);

        Status = ReprocessamentoLancamentosStatus.Failed;
        FailureReason = NormalizeReason(reason);
        FailedAt = now;
    }

    public void Cancel(string reason, DateTime now)
    {
        EnsureCanTransitionTo(ReprocessamentoLancamentosStatus.Canceled);

        Status = ReprocessamentoLancamentosStatus.Canceled;
        RejectionReason = NormalizeReason(reason);
        RejectedAt = now;
    }

    private void EnsureCanTransitionTo(ReprocessamentoLancamentosStatus targetStatus)
    {
        if (Status == targetStatus)
            throw new DomainException($"Solicitacao de reprocessamento ja esta em estado {targetStatus}.");

        if (IsFinal())
            throw new DomainException($"Solicitacao de reprocessamento em estado final {Status} nao pode mudar para {targetStatus}.");

        var allowed = (Status, targetStatus) switch
        {
            (ReprocessamentoLancamentosStatus.Pending, ReprocessamentoLancamentosStatus.Processing) => true,
            (ReprocessamentoLancamentosStatus.Pending, ReprocessamentoLancamentosStatus.Rejected) => true,
            (ReprocessamentoLancamentosStatus.Pending, ReprocessamentoLancamentosStatus.Canceled) => true,
            (ReprocessamentoLancamentosStatus.Pending, ReprocessamentoLancamentosStatus.Failed) => true,
            (ReprocessamentoLancamentosStatus.Processing, ReprocessamentoLancamentosStatus.Completed) => true,
            (ReprocessamentoLancamentosStatus.Processing, ReprocessamentoLancamentosStatus.CompletedWithWarnings) => true,
            (ReprocessamentoLancamentosStatus.Processing, ReprocessamentoLancamentosStatus.Rejected) => true,
            (ReprocessamentoLancamentosStatus.Processing, ReprocessamentoLancamentosStatus.Failed) => true,
            (ReprocessamentoLancamentosStatus.Processing, ReprocessamentoLancamentosStatus.Canceled) => true,
            _ => false
        };

        if (!allowed)
            throw new DomainException($"Transicao de reprocessamento invalida de {Status} para {targetStatus}.");
    }

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? "Erro nao especificado."
            : reason.Trim()[..Math.Min(500, reason.Trim().Length)];
}
