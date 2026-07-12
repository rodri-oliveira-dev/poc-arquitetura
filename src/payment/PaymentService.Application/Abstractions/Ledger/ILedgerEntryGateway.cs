using PaymentService.Domain.Payments;

namespace PaymentService.Application.Abstractions.Ledger;

public interface ILedgerEntryGateway
{
    Task<LedgerEntryCreationResult> CreateCreditAsync(
        LedgerCreditRequest request,
        CancellationToken cancellationToken);

    Task<LedgerReversalRequestResult> RequestReversalAsync(
        LedgerReversalRequest request,
        CancellationToken cancellationToken);
}

public sealed record LedgerCreditRequest(
    PaymentId PaymentId,
    MerchantId MerchantId,
    Money Amount,
    string Description,
    string ExternalReference,
    Guid IdempotencyKey,
    string? CorrelationId);

public sealed record LedgerReversalRequest(
    PaymentId PaymentId,
    RefundId RefundId,
    LedgerEntryReference OriginalLedgerEntryReference,
    string Reason,
    Guid IdempotencyKey,
    string? CorrelationId);

public sealed record LedgerEntryCreationResult(
    LedgerEntryCreationOutcome Outcome,
    LedgerEntryReference? LedgerEntryReference,
    LedgerEntryFailureCategory? FailureCategory,
    string? SafeError,
    TimeSpan? RetryAfter)
{
    public static LedgerEntryCreationResult Success(LedgerEntryReference ledgerEntryReference)
        => new(LedgerEntryCreationOutcome.Accepted, ledgerEntryReference, null, null, null);

    public static LedgerEntryCreationResult Transient(
        LedgerEntryFailureCategory category,
        string safeError,
        TimeSpan? retryAfter = null)
        => new(LedgerEntryCreationOutcome.TransientFailure, null, category, safeError, retryAfter);

    public static LedgerEntryCreationResult UnknownResult(string safeError)
        => new(LedgerEntryCreationOutcome.UnknownResult, null, LedgerEntryFailureCategory.Timeout, safeError, null);

    public static LedgerEntryCreationResult Definitive(
        LedgerEntryFailureCategory category,
        string safeError)
        => new(LedgerEntryCreationOutcome.DefinitiveFailure, null, category, safeError, null);
}

public enum LedgerEntryCreationOutcome
{
    Accepted,
    TransientFailure,
    UnknownResult,
    DefinitiveFailure
}

public enum LedgerEntryFailureCategory
{
    Timeout,
    RateLimited,
    ServiceUnavailable,
    Authentication,
    Authorization,
    Validation,
    IdempotencyConflict,
    NotFound,
    UnexpectedResponse,
    CircuitOpen,
    Network
}

public sealed record LedgerReversalRequestResult(
    LedgerEntryCreationOutcome Outcome,
    Guid? LedgerReversalId,
    LedgerEntryFailureCategory? FailureCategory,
    string? SafeError,
    TimeSpan? RetryAfter)
{
    public static LedgerReversalRequestResult Accepted(Guid ledgerReversalId)
        => new(LedgerEntryCreationOutcome.Accepted, ledgerReversalId, null, null, null);

    public static LedgerReversalRequestResult Transient(
        LedgerEntryFailureCategory category,
        string safeError,
        TimeSpan? retryAfter = null)
        => new(LedgerEntryCreationOutcome.TransientFailure, null, category, safeError, retryAfter);

    public static LedgerReversalRequestResult UnknownResult(string safeError)
        => new(LedgerEntryCreationOutcome.UnknownResult, null, LedgerEntryFailureCategory.Timeout, safeError, null);

    public static LedgerReversalRequestResult Definitive(
        LedgerEntryFailureCategory category,
        string safeError)
        => new(LedgerEntryCreationOutcome.DefinitiveFailure, null, category, safeError, null);
}
