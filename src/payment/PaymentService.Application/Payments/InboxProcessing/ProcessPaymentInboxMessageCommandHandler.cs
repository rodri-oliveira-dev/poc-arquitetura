using MediatR;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.InboxProcessing;

public sealed class ProcessPaymentInboxMessageCommandHandler(
    IPaymentInboxRepository inboxRepository,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IProviderEventMapper providerEventMapper,
    IClock clock,
    PaymentInboxProcessingOptions options)
    : IRequestHandler<ProcessPaymentInboxMessageCommand, ProcessPaymentInboxMessageResult>
{
    private const string DeadLetterOutcome = "dead_letter";

    private readonly IPaymentInboxRepository _inboxRepository = inboxRepository;
    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IProviderEventMapper _providerEventMapper = providerEventMapper;
    private readonly IClock _clock = clock;
    private readonly PaymentInboxProcessingOptions _options = options;

    public async Task<ProcessPaymentInboxMessageResult> Handle(
        ProcessPaymentInboxMessageCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var message = await _inboxRepository.GetByIdAsync(request.InboxMessageId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment InboxMessage {request.InboxMessageId} nao encontrada.");

        if (message.Status != PaymentInboxStatus.Processing ||
            !string.Equals(message.LockOwner, request.LockOwner, StringComparison.Ordinal))
        {
            return new ProcessPaymentInboxMessageResult(message.Id, message.Status, "claim_lost", false);
        }

        var now = _clock.UtcNow;

        if (message.EventCategory != StripeWebhookEventCategory.Supported)
        {
            message.MarkIgnored(now, "Provider event is outside Payment MVP scope.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProcessPaymentInboxMessageResult(message.Id, message.Status, "ignored", false);
        }

        var mapping = _providerEventMapper.Map(message);
        if (mapping.IsPermanentFailure || mapping.Event is null)
        {
            message.MarkDeadLetter(now, mapping.Reason ?? "Provider event payload is invalid.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProcessPaymentInboxMessageResult(message.Id, message.Status, DeadLetterOutcome, false);
        }

        var providerEvent = mapping.Event;
        var payment = await LoadPaymentForUpdateAsync(providerEvent, cancellationToken);

        if (payment is null)
        {
            ScheduleRetryOrDeadLetter(message, now, "Payment not found for provider reference.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProcessPaymentInboxMessageResult(message.Id, message.Status, ResolveMissingPaymentOutcome(message), false);
        }

        if (payment.Provider != providerEvent.Provider ||
            (payment.ExternalPaymentReference is not null && payment.ExternalPaymentReference != providerEvent.ProviderPaymentReference))
        {
            message.MarkDeadLetter(now, "Provider reference does not match local Payment.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProcessPaymentInboxMessageResult(message.Id, message.Status, DeadLetterOutcome, false);
        }

        var previousStatus = payment.Status;

        try
        {
            var changed = ApplyProviderEvent(payment, providerEvent, now);
            message.MarkProcessed(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ProcessPaymentInboxMessageResult(
                message.Id,
                message.Status,
                ResolveOutcome(previousStatus, payment.Status, providerEvent.Kind, changed),
                changed);
        }
        catch (DomainException exception)
        {
            message.MarkDeadLetter(now, BuildSafeDomainError(exception));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProcessPaymentInboxMessageResult(message.Id, message.Status, DeadLetterOutcome, false);
        }
    }

    private async Task<Payment?> LoadPaymentForUpdateAsync(
        PaymentProviderEvent providerEvent,
        CancellationToken cancellationToken)
    {
        return providerEvent.PaymentId is not null
            ? await _paymentRepository.GetByIdForUpdateAsync(providerEvent.PaymentId.Value, cancellationToken)
            : await _paymentRepository.GetByProviderReferenceForUpdateAsync(
            providerEvent.Provider,
            providerEvent.ProviderPaymentReference,
            cancellationToken);
    }

    private static bool ApplyProviderEvent(Payment payment, PaymentProviderEvent providerEvent, DateTimeOffset now)
        => providerEvent.Kind switch
        {
            PaymentProviderEventKind.Processing => payment.MarkProcessing(
                now,
                providerEvent.ProviderPaymentReference,
                providerEvent.ProviderStatus),
            PaymentProviderEventKind.Succeeded => payment.MarkSucceeded(
                now,
                providerEvent.ProviderPaymentReference,
                providerEvent.ProviderStatus,
                providerEvent.CorrelationId),
            PaymentProviderEventKind.Failed => payment.MarkFailed(now, providerEvent.ProviderStatus),
            PaymentProviderEventKind.Cancelled => payment.Cancel(now, providerEvent.ProviderStatus),
            PaymentProviderEventKind.RefundCreated => payment.RegisterRefundProviderCreated(
                now,
                providerEvent.RefundId ?? throw new InvalidOperationException("RefundId ausente no evento de refund."),
                providerEvent.ProviderRefundId ?? throw new InvalidOperationException("ProviderRefundId ausente no evento de refund."),
                providerEvent.ProviderStatus),
            PaymentProviderEventKind.RefundSucceeded => payment.MarkRefundProviderSucceeded(
                now,
                providerEvent.RefundId ?? throw new InvalidOperationException("RefundId ausente no evento de refund."),
                providerEvent.ProviderRefundId ?? throw new InvalidOperationException("ProviderRefundId ausente no evento de refund."),
                providerEvent.ProviderStatus),
            PaymentProviderEventKind.RefundFailed => payment.MarkRefundProviderFailed(
                now,
                providerEvent.RefundId ?? throw new InvalidOperationException("RefundId ausente no evento de refund."),
                providerEvent.ProviderRefundId ?? throw new InvalidOperationException("ProviderRefundId ausente no evento de refund."),
                providerEvent.ProviderStatus,
                "Provider reported refund failed."),
            _ => throw new InvalidOperationException("Provider event kind nao suportado.")
        };

    private void ScheduleRetryOrDeadLetter(PaymentInboxMessage message, DateTimeOffset now, string reason)
    {
        if (message.AttemptCount >= _options.MaxRetryCount)
        {
            message.MarkDeadLetter(now, "Payment not found for provider reference after maximum retries.");
            return;
        }

        var nextRetryAt = PaymentInboxRetryPolicy.CalculateNextRetryAt(
            now,
            message.AttemptCount,
            _options.BaseRetryDelay,
            _options.MaxRetryDelay);
        message.ScheduleRetry(now, nextRetryAt, reason);
    }

    private static string ResolveMissingPaymentOutcome(PaymentInboxMessage message)
        => message.Status == PaymentInboxStatus.DeadLetter ? DeadLetterOutcome : "retry_scheduled";

    private static string ResolveOutcome(
        PaymentStatus previousStatus,
        PaymentStatus currentStatus,
        PaymentProviderEventKind kind,
        bool changed)
    {
        if (changed)
            return "processed";

        var target = kind switch
        {
            PaymentProviderEventKind.Processing => PaymentStatus.Processing,
            PaymentProviderEventKind.Succeeded => PaymentStatus.Succeeded,
            PaymentProviderEventKind.Failed => PaymentStatus.Failed,
            PaymentProviderEventKind.Cancelled => PaymentStatus.Cancelled,
            PaymentProviderEventKind.RefundCreated or PaymentProviderEventKind.RefundSucceeded or PaymentProviderEventKind.RefundFailed => currentStatus,
            _ => throw new InvalidOperationException("Provider event kind nao suportado.")
        };

        return previousStatus == target ? "idempotent" : "regressive_ignored";
    }

    private static string BuildSafeDomainError(DomainException exception)
        => string.IsNullOrWhiteSpace(exception.Message)
            ? "Payment state transition rejected by domain state machine."
            : exception.Message;
}
