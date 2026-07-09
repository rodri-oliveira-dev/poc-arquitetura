using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Ledger;

public sealed class PaymentLedgerProcessor(
    IPaymentRepository paymentRepository,
    ILedgerEntryGateway ledgerEntryGateway,
    IUnitOfWork unitOfWork,
    IClock clock,
    PaymentLedgerProcessingOptions options) : IPaymentLedgerProcessor
{
    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly ILedgerEntryGateway _ledgerEntryGateway = ledgerEntryGateway;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IClock _clock = clock;
    private readonly PaymentLedgerProcessingOptions _options = options;

    public async Task<PaymentLedgerProcessorResult> ProcessBatchAsync(
        int batchSize,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);

        var now = _clock.UtcNow;
        var claimed = await _paymentRepository.ClaimLedgerIntegrationAsync(
            batchSize,
            now,
            lockOwner,
            leaseTimeout,
            cancellationToken);

        var completed = 0;
        var retryScheduled = 0;
        var definitive = 0;
        var deadLettered = 0;

        foreach (var payment in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = BuildRequest(payment);
            var result = await _ledgerEntryGateway.CreateCreditAsync(request, cancellationToken);
            var outcome = await PersistResultAsync(payment.PaymentId, lockOwner, result, cancellationToken);

            completed += outcome == PersistedOutcome.Completed ? 1 : 0;
            retryScheduled += outcome == PersistedOutcome.RetryScheduled ? 1 : 0;
            definitive += outcome == PersistedOutcome.FailedDefinitive ? 1 : 0;
            deadLettered += outcome == PersistedOutcome.DeadLettered ? 1 : 0;
        }

        return new PaymentLedgerProcessorResult(claimed.Count, completed, retryScheduled, definitive, deadLettered);
    }

    private static LedgerCreditRequest BuildRequest(Payment payment)
    {
        var idempotencyKey = PaymentLedgerIdempotencyKeyFactory.CreateForCredit(payment.PaymentId);
        return new LedgerCreditRequest(
            payment.PaymentId,
            payment.MerchantId,
            payment.Amount,
            "Payment captured",
            $"payment:{payment.PaymentId.Value}",
            idempotencyKey,
            payment.LedgerCorrelationId);
    }

    private async Task<PersistedOutcome> PersistResultAsync(
        PaymentId paymentId,
        string lockOwner,
        LedgerEntryCreationResult result,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var payment = await _paymentRepository.GetByIdForUpdateAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId.Value} nao encontrado ao persistir integracao Ledger.");

        if (!string.Equals(payment.LedgerLockOwner, lockOwner, StringComparison.Ordinal))
            return PersistedOutcome.None;

        var now = _clock.UtcNow;
        var outcome = ApplyResult(payment, result, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return outcome;
    }

    private PersistedOutcome ApplyResult(Payment payment, LedgerEntryCreationResult result, DateTimeOffset now)
    {
        if (result.Outcome == LedgerEntryCreationOutcome.Accepted && result.LedgerEntryReference is { } ledgerEntryReference)
        {
            payment.MarkCompleted(now, ledgerEntryReference);
            return PersistedOutcome.Completed;
        }

        if (result.Outcome is LedgerEntryCreationOutcome.TransientFailure or LedgerEntryCreationOutcome.UnknownResult)
        {
            if (payment.LedgerIntegrationAttemptCount >= _options.MaxRetryCount)
            {
                payment.MarkLedgerDeadLetter(now, result.SafeError ?? "Ledger integration retry limit reached.");
                return PersistedOutcome.DeadLettered;
            }

            var nextRetryAt = PaymentLedgerRetryPolicy.CalculateNextRetryAt(
                now,
                payment.LedgerIntegrationAttemptCount,
                _options.BaseRetryDelay,
                _options.MaxRetryDelay,
                result.RetryAfter);
            payment.ScheduleLedgerRetry(now, nextRetryAt, result.SafeError ?? "Transient Ledger integration failure.");
            return PersistedOutcome.RetryScheduled;
        }

        payment.MarkLedgerDefinitiveFailure(now, result.SafeError ?? "Definitive Ledger integration failure.");
        return PersistedOutcome.FailedDefinitive;
    }

    private enum PersistedOutcome
    {
        None,
        Completed,
        RetryScheduled,
        FailedDefinitive,
        DeadLettered
    }
}
