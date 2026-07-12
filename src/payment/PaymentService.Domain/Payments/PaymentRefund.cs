using PaymentService.Domain.Common;
using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public sealed class PaymentRefund : Entity
{
    public const int ReasonMaxLength = 100;
    public const int ExternalReferenceMaxLength = 200;
    public const int ProviderRefundIdMaxLength = 200;
    public const int ProviderStatusMaxLength = 100;
    public const int LastErrorMaxLength = 1000;
    public const int LockOwnerMaxLength = 200;

    private PaymentRefund()
    {
    }

    internal PaymentRefund(
        RefundId refundId,
        PaymentId paymentId,
        Money amount,
        string reason,
        string? externalReference,
        string? correlationId,
        DateTimeOffset now)
    {
        Id = refundId.Value;
        RefundId = refundId;
        PaymentId = paymentId;
        AmountValue = amount.Amount;
        Currency = amount.Currency;
        Reason = NormalizeRequired(reason, ReasonMaxLength, nameof(reason));
        ExternalReference = NormalizeOptional(externalReference, ExternalReferenceMaxLength, nameof(externalReference));
        CorrelationId = NormalizeOptional(correlationId, 100, nameof(correlationId));
        Status = RefundStatus.Requested;
        LedgerReversalStatus = LedgerIntegrationStatus.NotRequired;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public RefundId RefundId { get; private set; }

    public PaymentId PaymentId { get; private set; }

    public decimal AmountValue { get; private set; }

    public Currency Currency { get; private set; }

    public Money Amount => new(AmountValue, Currency);

    public string Reason { get; private set; } = string.Empty;

    public string? ExternalReference { get; private set; }

    public RefundStatus Status { get; private set; }

    public string? ProviderRefundId { get; private set; }

    public string? ProviderStatus { get; private set; }

    public Guid? LedgerReversalId { get; private set; }

    public LedgerIntegrationStatus LedgerReversalStatus { get; private set; }

    public int LedgerReversalAttemptCount { get; private set; }

    public DateTimeOffset? LedgerNextRetryAt { get; private set; }

    public string? LedgerLastError { get; private set; }

    public DateTimeOffset? LedgerProcessingStartedAt { get; private set; }

    public DateTimeOffset? LedgerLockedUntil { get; private set; }

    public string? LedgerLockOwner { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    internal bool RegisterProviderCreated(DateTimeOffset now, string providerRefundId, string? providerStatus)
    {
        if (Status is RefundStatus.ProviderSucceeded or RefundStatus.LedgerReversalPending or RefundStatus.Completed)
        {
            ApplyProviderData(providerRefundId, providerStatus);
            return false;
        }

        if (Status != RefundStatus.Requested && Status != RefundStatus.ProviderPending)
            throw new DomainException($"Refund {RefundId} nao pode registrar provider a partir de {Status}.");

        ApplyProviderData(providerRefundId, providerStatus);
        Status = RefundStatus.ProviderPending;
        UpdatedAt = now;
        return true;
    }

    internal bool MarkProviderSucceeded(DateTimeOffset now, string providerRefundId, string? providerStatus)
    {
        if (Status is RefundStatus.ProviderSucceeded or RefundStatus.LedgerReversalPending or RefundStatus.Completed)
        {
            ApplyProviderData(providerRefundId, providerStatus);
            return false;
        }

        if (Status is RefundStatus.ProviderFailed or RefundStatus.Failed or RefundStatus.DeadLetter)
            throw new DomainException($"Refund {RefundId} em estado {Status} nao pode ser confirmado pelo provider.");

        ApplyProviderData(providerRefundId, providerStatus);
        Status = RefundStatus.LedgerReversalPending;
        LedgerReversalStatus = LedgerIntegrationStatus.Pending;
        UpdatedAt = now;
        return true;
    }

    internal bool MarkProviderFailed(DateTimeOffset now, string providerRefundId, string? providerStatus, string reason)
    {
        if (Status is RefundStatus.ProviderFailed or RefundStatus.Failed)
            return false;

        if (Status is RefundStatus.LedgerReversalPending or RefundStatus.Completed)
            throw new DomainException($"Refund {RefundId} com estorno interno pendente/concluido nao pode regredir para falha no provider.");

        ApplyProviderData(providerRefundId, providerStatus);
        Status = RefundStatus.ProviderFailed;
        LedgerReversalStatus = LedgerIntegrationStatus.NotRequired;
        LedgerLastError = NormalizeOptional(reason, LastErrorMaxLength, nameof(reason));
        UpdatedAt = now;
        return true;
    }

    internal bool ClaimLedgerReversal(DateTimeOffset now, string lockOwner, DateTimeOffset lockedUntil)
    {
        if (Status != RefundStatus.LedgerReversalPending ||
            LedgerReversalStatus is LedgerIntegrationStatus.FailedDefinitive or LedgerIntegrationStatus.DeadLetter or LedgerIntegrationStatus.Completed ||
            LedgerReversalId is not null ||
            (LedgerLockedUntil is not null && LedgerLockedUntil > now) ||
            (LedgerNextRetryAt is not null && LedgerNextRetryAt > now))
        {
            return false;
        }

        LedgerReversalStatus = LedgerIntegrationStatus.Processing;
        LedgerReversalAttemptCount++;
        LedgerProcessingStartedAt = now;
        LedgerLockedUntil = lockedUntil;
        LedgerLockOwner = NormalizeRequired(lockOwner, LockOwnerMaxLength, nameof(lockOwner));
        LedgerLastError = null;
        UpdatedAt = now;
        return true;
    }

    internal void MarkLedgerReversalAccepted(DateTimeOffset now, Guid ledgerReversalId)
    {
        EnsureLedgerProcessing();
        LedgerReversalId = ledgerReversalId;
        LedgerReversalStatus = LedgerIntegrationStatus.Completed;
        Status = RefundStatus.Completed;
        CompletedAt = now;
        ClearLedgerProcessing();
        UpdatedAt = now;
    }

    internal void ScheduleLedgerRetry(DateTimeOffset now, DateTimeOffset nextRetryAt, string reason)
    {
        EnsureLedgerProcessing();
        LedgerReversalStatus = LedgerIntegrationStatus.RetryScheduled;
        LedgerNextRetryAt = nextRetryAt;
        LedgerLastError = NormalizeOptional(reason, LastErrorMaxLength, nameof(reason));
        ClearLedgerProcessing();
        UpdatedAt = now;
    }

    internal void MarkLedgerDeadLetter(DateTimeOffset now, string reason)
    {
        EnsureLedgerProcessing();
        LedgerReversalStatus = LedgerIntegrationStatus.DeadLetter;
        Status = RefundStatus.DeadLetter;
        LedgerLastError = NormalizeOptional(reason, LastErrorMaxLength, nameof(reason));
        LedgerNextRetryAt = null;
        ClearLedgerProcessing();
        UpdatedAt = now;
    }

    internal void MarkLedgerDefinitiveFailure(DateTimeOffset now, string reason)
    {
        EnsureLedgerProcessing();
        LedgerReversalStatus = LedgerIntegrationStatus.FailedDefinitive;
        Status = RefundStatus.Failed;
        LedgerLastError = NormalizeOptional(reason, LastErrorMaxLength, nameof(reason));
        LedgerNextRetryAt = null;
        ClearLedgerProcessing();
        UpdatedAt = now;
    }

    private void ApplyProviderData(string providerRefundId, string? providerStatus)
    {
        ProviderRefundId = NormalizeRequired(providerRefundId, ProviderRefundIdMaxLength, nameof(providerRefundId));
        ProviderStatus = NormalizeOptional(providerStatus, ProviderStatusMaxLength, nameof(providerStatus));
    }

    private void EnsureLedgerProcessing()
    {
        if (LedgerReversalStatus != LedgerIntegrationStatus.Processing)
            throw new DomainException("Estorno Ledger de refund deve estar em processamento.");
    }

    private void ClearLedgerProcessing()
    {
        LedgerProcessingStartedAt = null;
        LedgerLockedUntil = null;
        LedgerLockOwner = null;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
        => NormalizeOptional(value, maxLength, fieldName)
            ?? throw new DomainException($"{fieldName} e obrigatorio.");

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length > maxLength
            ? throw new DomainException($"{fieldName} deve ter no maximo {maxLength} caracteres.")
            : normalized;
    }
}
