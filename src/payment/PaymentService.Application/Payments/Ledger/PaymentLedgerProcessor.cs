using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Ledger;

public sealed class PaymentLedgerProcessor(
    IPaymentRepository paymentRepository,
    ILedgerEntryGateway ledgerEntryGateway,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    PaymentLedgerProcessingOptions options) : IPaymentLedgerProcessor
{
    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly ILedgerEntryGateway _ledgerEntryGateway = ledgerEntryGateway;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly PaymentLedgerProcessingOptions _options = options;

    public async Task<PaymentLedgerProcessorResult> ProcessBatchAsync(
        int batchSize,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);

        var now = _timeProvider.GetUtcNow();
        var claimed = await _paymentRepository.ClaimLedgerIntegrationAsync(
            batchSize,
            now,
            lockOwner,
            leaseTimeout,
            cancellationToken);

        var counters = new OutcomeCounters();

        await ProcessClaimedPaymentsAsync(claimed, lockOwner, counters, cancellationToken);

        var refundClaimed = await _paymentRepository.ClaimRefundLedgerReversalAsync(
            batchSize,
            _timeProvider.GetUtcNow(),
            lockOwner,
            leaseTimeout,
            cancellationToken);

        await ProcessClaimedRefundsAsync(refundClaimed, lockOwner, counters, cancellationToken);

        return new PaymentLedgerProcessorResult(
            claimed.Count + refundClaimed.Count,
            counters.Completed,
            counters.RetryScheduled,
            counters.FailedDefinitive,
            counters.DeadLettered);
    }

    private async Task ProcessClaimedPaymentsAsync(
        IEnumerable<Payment> claimed,
        string lockOwner,
        OutcomeCounters counters,
        CancellationToken cancellationToken)
    {
        foreach (var payment in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = BuildRequest(payment);
            var result = await _ledgerEntryGateway.CreateCreditAsync(request, cancellationToken);
            var outcome = await PersistResultAsync(payment.PaymentId, lockOwner, result, cancellationToken);
            counters.Record(outcome);
        }
    }

    private async Task ProcessClaimedRefundsAsync(
        IEnumerable<Payment> refundClaimed,
        string lockOwner,
        OutcomeCounters counters,
        CancellationToken cancellationToken)
    {
        foreach (var payment in refundClaimed)
        {
            foreach (var refund in payment.Refunds.Where(x =>
                x.Status == RefundStatus.LedgerReversalPending &&
                x.LedgerReversalStatus == LedgerIntegrationStatus.Processing &&
                string.Equals(x.LedgerLockOwner, lockOwner, StringComparison.Ordinal)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = BuildReversalRequest(payment, refund);
                var result = await _ledgerEntryGateway.RequestReversalAsync(request, cancellationToken);
                var outcome = await PersistRefundResultAsync(payment.PaymentId, refund.RefundId, lockOwner, result, cancellationToken);
                counters.Record(outcome);
            }
        }
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

    private static LedgerReversalRequest BuildReversalRequest(Payment payment, PaymentRefund refund)
    {
        var idempotencyKey = PaymentLedgerIdempotencyKeyFactory.CreateForRefundReversal(payment.PaymentId, refund.RefundId);
        return new LedgerReversalRequest(
            payment.PaymentId,
            refund.RefundId,
            payment.LedgerEntryReference ?? throw new InvalidOperationException("Refund exige lancamento original no Ledger."),
            $"Payment refund {refund.RefundId.Value:D}: {refund.Reason}",
            idempotencyKey,
            refund.CorrelationId);
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

        var now = _timeProvider.GetUtcNow();
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

    private async Task<PersistedOutcome> PersistRefundResultAsync(
        PaymentId paymentId,
        RefundId refundId,
        string lockOwner,
        LedgerReversalRequestResult result,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var payment = await _paymentRepository.GetByIdForUpdateAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId.Value} nao encontrado ao persistir estorno de refund.");
        var refund = payment.FindRefund(refundId)
            ?? throw new InvalidOperationException($"Refund {refundId.Value} nao encontrado ao persistir estorno.");

        if (!string.Equals(refund.LedgerLockOwner, lockOwner, StringComparison.Ordinal))
            return PersistedOutcome.None;

        var now = _timeProvider.GetUtcNow();
        var outcome = ApplyRefundResult(payment, refund, result, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return outcome;
    }

    private PersistedOutcome ApplyRefundResult(
        Payment payment,
        PaymentRefund refund,
        LedgerReversalRequestResult result,
        DateTimeOffset now)
    {
        if (result.Outcome == LedgerEntryCreationOutcome.Accepted && result.LedgerReversalId is { } ledgerReversalId)
        {
            payment.MarkRefundLedgerReversalAccepted(refund.RefundId, now, ledgerReversalId);
            return PersistedOutcome.Completed;
        }

        if (result.Outcome is LedgerEntryCreationOutcome.TransientFailure or LedgerEntryCreationOutcome.UnknownResult)
        {
            if (refund.LedgerReversalAttemptCount >= _options.MaxRetryCount)
            {
                payment.MarkRefundLedgerDeadLetter(refund.RefundId, now, result.SafeError ?? "Ledger reversal retry limit reached.");
                return PersistedOutcome.DeadLettered;
            }

            var nextRetryAt = PaymentLedgerRetryPolicy.CalculateNextRetryAt(
                now,
                refund.LedgerReversalAttemptCount,
                _options.BaseRetryDelay,
                _options.MaxRetryDelay,
                result.RetryAfter);
            payment.ScheduleRefundLedgerRetry(refund.RefundId, now, nextRetryAt, result.SafeError ?? "Transient Ledger reversal failure.");
            return PersistedOutcome.RetryScheduled;
        }

        payment.MarkRefundLedgerDefinitiveFailure(refund.RefundId, now, result.SafeError ?? "Definitive Ledger reversal failure.");
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

    private sealed class OutcomeCounters
    {
        public int Completed
        {
            get; private set;
        }

        public int RetryScheduled
        {
            get; private set;
        }

        public int FailedDefinitive
        {
            get; private set;
        }

        public int DeadLettered
        {
            get; private set;
        }

        public void Record(PersistedOutcome outcome)
        {
            switch (outcome)
            {
                case PersistedOutcome.Completed:
                    Completed++;
                    break;
                case PersistedOutcome.RetryScheduled:
                    RetryScheduled++;
                    break;
                case PersistedOutcome.FailedDefinitive:
                    FailedDefinitive++;
                    break;
                case PersistedOutcome.DeadLettered:
                    DeadLettered++;
                    break;
                case PersistedOutcome.None:
                    break;
                default:
                    break;
            }
        }
    }
}
