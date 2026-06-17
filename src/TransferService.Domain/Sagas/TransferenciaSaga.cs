using TransferService.Domain.Common;
using TransferService.Domain.Exceptions;

namespace TransferService.Domain.Sagas;

public sealed class TransferenciaSaga : Entity, IAggregateRoot
{
    public MerchantId SourceMerchantId { get; private set; }
    public MerchantId DestinationMerchantId { get; private set; }
    public TransferAmount Amount { get; private set; }
    public string? Description { get; private set; }
    public string? ExternalReference { get; private set; }
    public TransferenciaSagaStatus Status { get; private set; }
    public TransferenciaSagaStep Step { get; private set; }
    public Guid? DebitLancamentoId { get; private set; }
    public Guid? CreditLancamentoId { get; private set; }
    public Guid? CompensationEstornoId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? IdempotencyPayloadHash { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? ProcessingLockOwner { get; private set; }
    public DateTimeOffset? ProcessingLockedUntil { get; private set; }
    public bool DebitCreated { get; private set; }
    public bool CreditCreated { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private TransferenciaSaga()
    {
    }

    public TransferenciaSaga(
        MerchantId sourceMerchantId,
        MerchantId destinationMerchantId,
        TransferAmount amount,
        DateTimeOffset now)
    {
        if (sourceMerchantId == destinationMerchantId)
            throw new DomainException("SourceMerchantId nao pode ser igual ao DestinationMerchantId.");

        SourceMerchantId = sourceMerchantId;
        DestinationMerchantId = destinationMerchantId;
        Amount = amount;
        Status = TransferenciaSagaStatus.Pending;
        Step = TransferenciaSagaStep.Created;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void RegisterRequestMetadata(
        string idempotencyKey,
        string idempotencyPayloadHash,
        string? correlationId,
        DateTimeOffset now,
        string? description = null,
        string? externalReference = null)
    {
        IdempotencyKey = NormalizeRequired(idempotencyKey, nameof(idempotencyKey));
        IdempotencyPayloadHash = NormalizeRequired(idempotencyPayloadHash, nameof(idempotencyPayloadHash));
        CorrelationId = Normalize(correlationId);
        Description = Normalize(description);
        ExternalReference = Normalize(externalReference);
        UpdatedAt = now;
    }

    public void StartProcessing(DateTimeOffset now)
    {
        EnsureNotFinalized();
        EnsureStatus(TransferenciaSagaStatus.Pending, "Saga somente pode iniciar processamento quando estiver pendente.");

        MoveTo(TransferenciaSagaStatus.Processing, TransferenciaSagaStep.Processing, now);
    }

    public void MarkDebitCreating(DateTimeOffset now)
    {
        EnsureNotFinalized();
        EnsureStatus(TransferenciaSagaStatus.Processing, "Debito somente pode ser criado apos inicio do processamento.");

        MoveTo(TransferenciaSagaStatus.DebitCreating, TransferenciaSagaStep.DebitCreation, now);
    }

    public void MarkDebitCreated(DateTimeOffset now, Guid? debitLancamentoId = null)
    {
        EnsureNotFinalized();
        EnsureStatus(TransferenciaSagaStatus.DebitCreating, "Debito somente pode ser marcado como criado quando estiver em criacao.");

        DebitLancamentoId = debitLancamentoId ?? DebitLancamentoId;
        DebitCreated = true;
        MoveTo(TransferenciaSagaStatus.DebitCreated, TransferenciaSagaStep.DebitCreated, now);
    }

    public void MarkCreditCreating(DateTimeOffset now)
    {
        EnsureNotFinalized();
        EnsureStatus(TransferenciaSagaStatus.DebitCreated, "Credito somente pode ser criado apos debito criado.");

        if (!DebitCreated)
            throw new DomainException("Credito somente pode ser criado apos debito criado.");

        MoveTo(TransferenciaSagaStatus.CreditCreating, TransferenciaSagaStep.CreditCreation, now);
    }

    public void MarkCompleted(DateTimeOffset now, Guid? creditLancamentoId = null)
    {
        EnsureNotFinalized();
        EnsureStatus(TransferenciaSagaStatus.CreditCreating, "Saga somente pode ser concluida durante criacao do credito.");

        if (!DebitCreated)
            throw new DomainException("Saga nao pode ser concluida sem debito criado.");

        CreditLancamentoId = creditLancamentoId ?? CreditLancamentoId;
        CreditCreated = true;
        MoveTo(TransferenciaSagaStatus.Completed, TransferenciaSagaStep.Completed, now);
    }

    public void MarkCompensationRequested(DateTimeOffset now, Guid? compensationEstornoId = null)
    {
        EnsureNotFinalized();

        if (!DebitCreated)
            throw new DomainException("Compensacao somente pode ser solicitada apos debito criado.");

        CompensationEstornoId = compensationEstornoId ?? CompensationEstornoId;
        MoveTo(TransferenciaSagaStatus.CompensationRequested, TransferenciaSagaStep.Compensation, now);
    }

    public void MarkCompensated(DateTimeOffset now, Guid? compensationEstornoId = null)
    {
        EnsureNotFinalized();
        EnsureStatus(TransferenciaSagaStatus.CompensationRequested, "Saga somente pode ser compensada apos solicitacao de compensacao.");

        CompensationEstornoId = compensationEstornoId ?? CompensationEstornoId;
        MoveTo(TransferenciaSagaStatus.Compensated, TransferenciaSagaStep.Compensated, now);
    }

    public void MarkFailed(DateTimeOffset now, string? failureReason = null)
    {
        EnsureNotFinalized();

        FailureReason = Normalize(failureReason);
        MoveTo(TransferenciaSagaStatus.Failed, TransferenciaSagaStep.Failed, now);
    }

    public void MarkRejected(DateTimeOffset now)
    {
        EnsureNotFinalized();

        if (DebitCreated)
            throw new DomainException("Saga com debito criado deve ser compensada, nao rejeitada.");

        MoveTo(TransferenciaSagaStatus.Rejected, TransferenciaSagaStep.Rejected, now);
    }

    public void ScheduleRetry(DateTimeOffset nextRetryAt, string? failureReason, DateTimeOffset now)
    {
        EnsureNotFinalized();

        RetryCount++;
        NextRetryAt = nextRetryAt;
        FailureReason = Normalize(failureReason);
        UpdatedAt = now;
    }

    public void MarkClaimedForProcessing(string lockOwner, DateTimeOffset lockedUntil, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);
        EnsureNotFinalized();

        ProcessingLockOwner = lockOwner.Trim();
        ProcessingLockedUntil = lockedUntil;
        if (Status == TransferenciaSagaStatus.Pending)
            Status = TransferenciaSagaStatus.Processing;
        Step = TransferenciaSagaStep.Processing;
        UpdatedAt = now;
    }

    private void MoveTo(TransferenciaSagaStatus status, TransferenciaSagaStep step, DateTimeOffset now)
    {
        Status = status;
        Step = step;
        UpdatedAt = now;
        if (status is TransferenciaSagaStatus.Completed
            or TransferenciaSagaStatus.Compensated
            or TransferenciaSagaStatus.Failed
            or TransferenciaSagaStatus.Rejected)
            CompletedAt = now;
    }

    private void EnsureNotFinalized()
    {
        if (Status is TransferenciaSagaStatus.Completed
            or TransferenciaSagaStatus.Compensated
            or TransferenciaSagaStatus.Failed
            or TransferenciaSagaStatus.Rejected)
            throw new DomainException("Saga finalizada nao pode ser alterada.");
    }

    private void EnsureStatus(TransferenciaSagaStatus expected, string message)
    {
        if (Status != expected)
            throw new DomainException(message);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequired(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }
}
