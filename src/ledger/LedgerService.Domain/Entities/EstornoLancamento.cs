using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class EstornoLancamento : Entity, IAggregateRoot
{
    private static readonly EstornoLancamentoStatus[] FinalStatuses =
    [
        EstornoLancamentoStatus.Completed,
        EstornoLancamentoStatus.Rejected,
        EstornoLancamentoStatus.Failed
    ];

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

    public bool IsFinal()
        => FinalStatuses.Contains(Status);

    public void MarkProcessing(DateTime now)
    {
        if (Status == EstornoLancamentoStatus.Processing)
        {
            ProcessingStartedAt ??= now;
            return;
        }

        EnsureCanTransitionTo(EstornoLancamentoStatus.Processing);

        Status = EstornoLancamentoStatus.Processing;
        ProcessingStartedAt ??= now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void Complete(Guid lancamentoCompensatorioId, DateTime now)
    {
        if (lancamentoCompensatorioId == Guid.Empty)
            throw new DomainException("LancamentoCompensatorioId e obrigatorio.");

        EnsureCanTransitionTo(EstornoLancamentoStatus.Completed);

        LancamentoCompensatorioId = lancamentoCompensatorioId;
        Status = EstornoLancamentoStatus.Completed;
        CompletedAt = now;
        FailureReason = null;
        RejectionReason = null;
    }

    public void Reject(string reason, DateTime now)
    {
        EnsureCanTransitionTo(EstornoLancamentoStatus.Rejected);

        Status = EstornoLancamentoStatus.Rejected;
        RejectionReason = NormalizeReason(reason);
        RejectedAt = now;
    }

    public void Fail(string reason, DateTime now)
    {
        EnsureCanTransitionTo(EstornoLancamentoStatus.Failed);

        Status = EstornoLancamentoStatus.Failed;
        FailureReason = NormalizeReason(reason);
        FailedAt = now;
    }

    private void EnsureCanTransitionTo(EstornoLancamentoStatus targetStatus)
    {
        if (Status == targetStatus)
            throw new DomainException($"Solicitacao de estorno ja esta em estado {targetStatus}.");

        if (IsFinal())
            throw new DomainException($"Solicitacao de estorno em estado final {Status} nao pode mudar para {targetStatus}.");

        var allowed = (Status, targetStatus) switch
        {
            (EstornoLancamentoStatus.Pending, EstornoLancamentoStatus.Processing) => true,
            (EstornoLancamentoStatus.Pending, EstornoLancamentoStatus.Rejected) => true,
            (EstornoLancamentoStatus.Pending, EstornoLancamentoStatus.Failed) => true,
            (EstornoLancamentoStatus.Processing, EstornoLancamentoStatus.Completed) => true,
            (EstornoLancamentoStatus.Processing, EstornoLancamentoStatus.Rejected) => true,
            (EstornoLancamentoStatus.Processing, EstornoLancamentoStatus.Failed) => true,
            _ => false
        };

        if (!allowed)
            throw new DomainException($"Transicao de estorno invalida de {Status} para {targetStatus}.");
    }

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? "Erro nao especificado."
            : reason.Trim()[..Math.Min(500, reason.Trim().Length)];
}
