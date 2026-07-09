using PaymentService.Domain.Common;
using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public sealed class Payment : Entity, IAggregateRoot
{
    public const int DescriptionMaxLength = 500;
    public const int ProviderStatusMaxLength = 100;

    public PaymentId PaymentId
    {
        get; private set;
    }

    public MerchantId MerchantId
    {
        get; private set;
    }

    public decimal AmountValue
    {
        get; private set;
    }

    public Currency Currency
    {
        get; private set;
    }

    public Money Amount => new(AmountValue, Currency);

    public PaymentProvider Provider
    {
        get; private set;
    }

    public PaymentStatus Status
    {
        get; private set;
    }

    public ExternalReference? ExternalReference
    {
        get; private set;
    }

    public ExternalPaymentReference? ExternalPaymentReference
    {
        get; private set;
    }

    public LedgerEntryReference? LedgerEntryReference
    {
        get; private set;
    }

    public LedgerIntegrationStatus LedgerIntegrationStatus
    {
        get; private set;
    }

    public int LedgerIntegrationAttemptCount
    {
        get; private set;
    }

    public DateTimeOffset? LedgerNextRetryAt
    {
        get; private set;
    }

    public string? LedgerLastError
    {
        get; private set;
    }

    public DateTimeOffset? LedgerProcessingStartedAt
    {
        get; private set;
    }

    public DateTimeOffset? LedgerLockedUntil
    {
        get; private set;
    }

    public string? LedgerLockOwner
    {
        get; private set;
    }

    public string? LedgerCorrelationId
    {
        get; private set;
    }

    public string? ProviderStatus
    {
        get; private set;
    }

    public string? Description
    {
        get; private set;
    }

    public DateTimeOffset CreatedAt
    {
        get; private set;
    }

    public DateTimeOffset UpdatedAt
    {
        get; private set;
    }

    public DateTimeOffset? CompletedAt
    {
        get; private set;
    }

    private Payment()
    {
    }

    public Payment(
        PaymentId paymentId,
        MerchantId merchantId,
        Money amount,
        PaymentProvider provider,
        DateTimeOffset now,
        string? description = null,
        ExternalReference? externalReference = null)
    {
        if (!Enum.IsDefined(provider))
            throw new DomainException("PaymentProvider invalido.");

        Id = paymentId.Value;
        PaymentId = paymentId;
        MerchantId = merchantId;
        AmountValue = amount.Amount;
        Currency = amount.Currency;
        Provider = provider;
        Status = PaymentStatus.Pending;
        LedgerIntegrationStatus = LedgerIntegrationStatus.NotRequired;
        Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description));
        ExternalReference = externalReference;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public bool MarkRequiresAction(DateTimeOffset now, ExternalPaymentReference? reference = null, string? providerStatus = null)
        => MoveByProviderEvent(
            [PaymentStatus.Pending, PaymentStatus.Processing],
            PaymentStatus.RequiresAction,
            now,
            reference,
            providerStatus,
            "ProviderRequiresAction");

    public bool RegisterProviderIntent(DateTimeOffset now, ExternalPaymentReference reference, string? providerStatus = null)
    {
        if (IsFinal(Status))
            throw new DomainException($"Payment em estado final {Status} nao pode registrar referencia externa.");

        ApplyProviderData(reference, providerStatus);
        UpdatedAt = now;
        return true;
    }

    public bool MarkProcessing(DateTimeOffset now, ExternalPaymentReference? reference = null, string? providerStatus = null)
        => MoveByProviderEvent(
            [PaymentStatus.Pending, PaymentStatus.RequiresAction],
            PaymentStatus.Processing,
            now,
            reference,
            providerStatus,
            "ProviderProcessing");

    public bool MarkSucceeded(
        DateTimeOffset now,
        ExternalPaymentReference? reference = null,
        string? providerStatus = null,
        string? correlationId = null)
    {
        var changed = MoveByProviderEvent(
            [PaymentStatus.Pending, PaymentStatus.RequiresAction, PaymentStatus.Processing],
            PaymentStatus.Succeeded,
            now,
            reference,
            providerStatus,
            "ProviderSucceeded");

        if (Status == PaymentStatus.Succeeded && LedgerEntryReference is null)
        {
            LedgerIntegrationStatus = LedgerIntegrationStatus.Pending;
            LedgerCorrelationId ??= NormalizeOptional(correlationId, 100, nameof(correlationId));
        }

        return changed;
    }

    public bool MarkFailed(DateTimeOffset now, string? providerStatus = null)
        => MoveToTerminalProviderState(
            [PaymentStatus.Pending, PaymentStatus.RequiresAction, PaymentStatus.Processing],
            PaymentStatus.Failed,
            now,
            providerStatus,
            "ProviderFailed");

    public bool Cancel(DateTimeOffset now, string? providerStatus = null)
        => MoveToTerminalProviderState(
            [PaymentStatus.Pending, PaymentStatus.RequiresAction, PaymentStatus.Processing],
            PaymentStatus.Cancelled,
            now,
            providerStatus,
            "ProviderCancelled");

    public bool MarkLedgerEntryRequested(DateTimeOffset now)
    {
        if (Status == PaymentStatus.LedgerPending)
            return false;

        EnsureStatus(PaymentStatus.Succeeded, "LedgerEntryRequested somente e permitido para Payment Succeeded.");
        MoveTo(PaymentStatus.LedgerPending, now);
        LedgerIntegrationStatus = LedgerIntegrationStatus.Processing;
        return true;
    }

    public bool ClaimLedgerIntegration(DateTimeOffset now, string lockOwner, DateTimeOffset lockedUntil)
    {
        if (LedgerEntryReference is not null || Status == PaymentStatus.Completed)
            return false;

        if (LedgerIntegrationStatus is LedgerIntegrationStatus.FailedDefinitive or LedgerIntegrationStatus.DeadLetter)
            return false;

        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.LedgerPending))
            return false;

        if (LedgerLockedUntil is not null && LedgerLockedUntil > now)
            return false;

        if (LedgerNextRetryAt is not null && LedgerNextRetryAt > now)
            return false;

        if (Status == PaymentStatus.Succeeded)
            MoveTo(PaymentStatus.LedgerPending, now);
        else
            UpdatedAt = now;

        LedgerIntegrationStatus = LedgerIntegrationStatus.Processing;
        LedgerIntegrationAttemptCount++;
        LedgerProcessingStartedAt = now;
        LedgerLockedUntil = lockedUntil;
        LedgerLockOwner = NormalizeRequired(lockOwner, 200, nameof(lockOwner));
        LedgerLastError = null;
        return true;
    }

    public bool MarkCompleted(DateTimeOffset now, LedgerEntryReference ledgerEntryReference)
    {
        if (Status == PaymentStatus.Completed)
        {
            return LedgerEntryReference != ledgerEntryReference
                ? throw new DomainException("Payment Completed nao pode trocar LedgerEntryReference.")
                : false;
        }

        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.LedgerPending))
            throw new DomainException("LedgerEntryCreated somente e permitido para Payment Succeeded ou LedgerPending.");

        LedgerEntryReference = ledgerEntryReference;
        MoveTo(PaymentStatus.Completed, now);
        LedgerIntegrationStatus = LedgerIntegrationStatus.Completed;
        LedgerNextRetryAt = null;
        LedgerLastError = null;
        LedgerProcessingStartedAt = null;
        LedgerLockedUntil = null;
        LedgerLockOwner = null;
        CompletedAt = now;
        return true;
    }

    public void ScheduleLedgerRetry(DateTimeOffset now, DateTimeOffset nextRetryAt, string reason)
    {
        EnsureLedgerProcessing("Ledger retry somente pode ser agendado para integracao em processamento.");
        LedgerIntegrationStatus = LedgerIntegrationStatus.RetryScheduled;
        LedgerNextRetryAt = nextRetryAt;
        LedgerLastError = NormalizeOptional(reason, 1000, nameof(reason));
        LedgerProcessingStartedAt = null;
        LedgerLockedUntil = null;
        LedgerLockOwner = null;
        UpdatedAt = now;
    }

    public void MarkLedgerDeadLetter(DateTimeOffset now, string reason)
    {
        EnsureLedgerProcessing("Ledger dead letter somente pode ser marcado para integracao em processamento.");
        LedgerIntegrationStatus = LedgerIntegrationStatus.DeadLetter;
        LedgerNextRetryAt = null;
        LedgerLastError = NormalizeOptional(reason, 1000, nameof(reason));
        LedgerProcessingStartedAt = null;
        LedgerLockedUntil = null;
        LedgerLockOwner = null;
        UpdatedAt = now;
    }

    public void MarkLedgerDefinitiveFailure(DateTimeOffset now, string reason)
    {
        EnsureLedgerProcessing("Falha definitiva de Ledger somente pode ser marcada para integracao em processamento.");
        LedgerIntegrationStatus = LedgerIntegrationStatus.FailedDefinitive;
        LedgerNextRetryAt = null;
        LedgerLastError = NormalizeOptional(reason, 1000, nameof(reason));
        LedgerProcessingStartedAt = null;
        LedgerLockedUntil = null;
        LedgerLockOwner = null;
        UpdatedAt = now;
    }

    private bool MoveByProviderEvent(
        PaymentStatus[] allowedSources,
        PaymentStatus destination,
        DateTimeOffset now,
        ExternalPaymentReference? reference,
        string? providerStatus,
        string eventName)
    {
        if (Status == destination)
        {
            ApplyProviderData(reference, providerStatus);
            return false;
        }

        if (IsFinal(Status))
            throw new DomainException($"{eventName} nao pode alterar Payment em estado final {Status}.");

        if (!allowedSources.Contains(Status))
        {
            return ProgressRank(Status) > ProgressRank(destination)
                ? false
                : throw new DomainException($"{eventName} nao e permitido a partir de {Status}.");
        }

        ApplyProviderData(reference, providerStatus);
        MoveTo(destination, now);
        return true;
    }

    private bool MoveToTerminalProviderState(
        PaymentStatus[] allowedSources,
        PaymentStatus destination,
        DateTimeOffset now,
        string? providerStatus,
        string eventName)
    {
        if (Status == destination)
        {
            ApplyProviderData(null, providerStatus);
            return false;
        }

        if (Status is PaymentStatus.Succeeded)
            return false;

        if (Status is PaymentStatus.LedgerPending or PaymentStatus.Completed)
            throw new DomainException($"{eventName} nao pode regredir Payment {Status}.");

        if (!allowedSources.Contains(Status))
            throw new DomainException($"{eventName} nao e permitido a partir de {Status}.");

        ApplyProviderData(null, providerStatus);
        MoveTo(destination, now);
        CompletedAt = now;
        return true;
    }

    private void MoveTo(PaymentStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }

    private void ApplyProviderData(ExternalPaymentReference? reference, string? providerStatus)
    {
        if (reference is not null)
            ExternalPaymentReference = reference;

        ProviderStatus = NormalizeOptional(providerStatus, ProviderStatusMaxLength, nameof(providerStatus));
    }

    private void EnsureStatus(PaymentStatus expected, string message)
    {
        if (Status != expected)
            throw new DomainException(message);
    }

    private void EnsureLedgerProcessing(string message)
    {
        if (LedgerIntegrationStatus != LedgerIntegrationStatus.Processing)
            throw new DomainException(message);
    }

    private static int ProgressRank(PaymentStatus status)
        => status switch
        {
            PaymentStatus.Pending => 1,
            PaymentStatus.RequiresAction => 2,
            PaymentStatus.Processing => 3,
            PaymentStatus.Succeeded => 4,
            PaymentStatus.LedgerPending => 5,
            PaymentStatus.Completed => 6,
            PaymentStatus.Failed => throw new NotImplementedException(),
            PaymentStatus.Cancelled => throw new NotImplementedException(),
            _ => 0
        };

    private static bool IsFinal(PaymentStatus status)
        => status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Cancelled;

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length > maxLength
            ? throw new DomainException($"{fieldName} deve ter no maximo {maxLength} caracteres.")
            : normalized;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
        => NormalizeOptional(value, maxLength, fieldName)
            ?? throw new DomainException($"{fieldName} e obrigatorio.");
}
