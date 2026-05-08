using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class ReprocessamentoLancamentos : Entity, IAggregateRoot
{
    public string MerchantId { get; private set; }
    public DateOnly DataInicial { get; private set; }
    public DateOnly DataFinal { get; private set; }
    public string Motivo { get; private set; }
    public ReprocessamentoLancamentosStatus Status { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public string? RejectionReason { get; private set; }

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
        Guid correlationId)
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
        CreatedAt = DateTime.Now;
    }

    public bool IsFinal()
        => Status is ReprocessamentoLancamentosStatus.Completed
            or ReprocessamentoLancamentosStatus.CompletedWithWarnings
            or ReprocessamentoLancamentosStatus.Failed
            or ReprocessamentoLancamentosStatus.Rejected
            or ReprocessamentoLancamentosStatus.Canceled;

    public void MarkProcessing(DateTime now)
    {
        if (IsFinal())
            return;

        if (Status is not ReprocessamentoLancamentosStatus.Pending
            and not ReprocessamentoLancamentosStatus.Processing)
            throw new DomainException("Solicitacao de reprocessamento nao esta pendente para processamento.");

        Status = ReprocessamentoLancamentosStatus.Processing;
        ProcessingStartedAt ??= now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void Complete(DateTime now)
    {
        if (Status == ReprocessamentoLancamentosStatus.Completed)
            return;

        if (Status != ReprocessamentoLancamentosStatus.Processing)
            throw new DomainException("Solicitacao de reprocessamento deve estar em processamento para ser concluida.");

        Status = ReprocessamentoLancamentosStatus.Completed;
        CompletedAt = now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void CompleteWithWarnings(string reason, DateTime now)
    {
        if (Status == ReprocessamentoLancamentosStatus.CompletedWithWarnings)
            return;

        if (Status != ReprocessamentoLancamentosStatus.Processing)
            throw new DomainException("Solicitacao de reprocessamento deve estar em processamento para ser concluida com avisos.");

        Status = ReprocessamentoLancamentosStatus.CompletedWithWarnings;
        CompletedAt = now;
        FailureReason = NormalizeReason(reason);
        RejectionReason = null;
    }

    public void Reject(string reason, DateTime now)
    {
        if (Status == ReprocessamentoLancamentosStatus.Completed)
            throw new DomainException("Solicitacao de reprocessamento concluida nao pode ser rejeitada.");

        Status = ReprocessamentoLancamentosStatus.Rejected;
        RejectionReason = NormalizeReason(reason);
        RejectedAt = now;
    }

    public void Fail(string reason, DateTime now)
    {
        if (Status == ReprocessamentoLancamentosStatus.Completed)
            throw new DomainException("Solicitacao de reprocessamento concluida nao pode ser marcada como falha.");

        Status = ReprocessamentoLancamentosStatus.Failed;
        FailureReason = NormalizeReason(reason);
        FailedAt = now;
    }

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? "Erro nao especificado."
            : reason.Trim()[..Math.Min(500, reason.Trim().Length)];
}
