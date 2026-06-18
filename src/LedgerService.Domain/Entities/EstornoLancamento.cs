using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class EstornoLancamento : Entity, IAggregateRoot
{
    public Guid LancamentoOriginalId
    {
        get; private set;
    }
    public string MerchantId
    {
        get; private set;
    }
    public string Motivo
    {
        get; private set;
    }
    public EstornoLancamentoStatus Status
    {
        get; private set;
    }
    public Guid? LancamentoCompensatorioId
    {
        get; private set;
    }
    public string? RejectionReason
    {
        get; private set;
    }
    public string? FailureReason
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
    public DateTime? RejectedAt
    {
        get; private set;
    }
    public DateTime? FailedAt
    {
        get; private set;
    }

    private EstornoLancamento()
    {
        MerchantId = string.Empty;
        Motivo = string.Empty;
    }

    public EstornoLancamento(
        Guid lancamentoOriginalId,
        string merchantId,
        string motivo,
        Guid correlationId,
        DateTime createdAt)
    {
        if (lancamentoOriginalId == Guid.Empty)
            throw new DomainException("LancamentoOriginalId e obrigatorio.");

        LancamentoOriginalId = lancamentoOriginalId;
        MerchantId = string.IsNullOrWhiteSpace(merchantId)
            ? throw new DomainException("MerchantId e obrigatorio.")
            : merchantId.Trim();
        Motivo = string.IsNullOrWhiteSpace(motivo)
            ? throw new DomainException("Motivo e obrigatorio.")
            : motivo.Trim();
        Status = EstornoLancamentoStatus.Pending;
        CorrelationId = correlationId;
        CreatedAt = createdAt;
    }

    public bool IsActive()
        => Status is EstornoLancamentoStatus.Pending or EstornoLancamentoStatus.Processing;

    public bool IsCompleted()
        => Status == EstornoLancamentoStatus.Completed;

    public void MarkProcessing(DateTime now)
    {
        if (Status == EstornoLancamentoStatus.Completed)
            return;

        if (Status != EstornoLancamentoStatus.Pending && Status != EstornoLancamentoStatus.Processing)
            throw new DomainException("Solicitacao de estorno nao esta pendente para processamento.");

        Status = EstornoLancamentoStatus.Processing;
        ProcessingStartedAt ??= now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void Complete(Guid lancamentoCompensatorioId, DateTime now)
    {
        if (lancamentoCompensatorioId == Guid.Empty)
            throw new DomainException("LancamentoCompensatorioId e obrigatorio.");

        if (Status == EstornoLancamentoStatus.Completed)
            return;

        if (Status != EstornoLancamentoStatus.Processing)
            throw new DomainException("Solicitacao de estorno deve estar em processamento para ser concluida.");

        LancamentoCompensatorioId = lancamentoCompensatorioId;
        Status = EstornoLancamentoStatus.Completed;
        CompletedAt = now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void Reject(string reason, DateTime now)
    {
        if (Status == EstornoLancamentoStatus.Completed)
            throw new DomainException("Solicitacao de estorno concluida nao pode ser rejeitada.");

        Status = EstornoLancamentoStatus.Rejected;
        RejectionReason = NormalizeReason(reason);
        RejectedAt = now;
    }

    public void Fail(string reason, DateTime now)
    {
        if (Status == EstornoLancamentoStatus.Completed)
            throw new DomainException("Solicitacao de estorno concluida nao pode ser marcada como falha.");

        Status = EstornoLancamentoStatus.Failed;
        FailureReason = NormalizeReason(reason);
        FailedAt = now;
    }

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? "Erro nao especificado."
            : reason.Trim()[..Math.Min(500, reason.Trim().Length)];
}
