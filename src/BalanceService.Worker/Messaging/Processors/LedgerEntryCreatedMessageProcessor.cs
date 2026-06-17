using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Contracts;
using BalanceService.Worker.Observability;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BalanceService.Worker.Messaging.Processors;

public sealed partial class LedgerEntryCreatedMessageProcessor
{
    public const string ActivitySourceName = "BalanceService.MessageProcessor";

    private const string LegacyV1CurrencyFallback = "BRL";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly IDeadLetterPublisher _deadLetterPublisher;
    private readonly MessagingMetrics _metrics;
    private readonly ILogger<LedgerEntryCreatedMessageProcessor> _logger;

    public LedgerEntryCreatedMessageProcessor(
        IServiceProvider serviceProvider,
        IDeadLetterPublisher deadLetterPublisher,
        MessagingMetrics metrics,
        ILogger<LedgerEntryCreatedMessageProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _deadLetterPublisher = deadLetterPublisher;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(ReceivedMessage message, CancellationToken cancellationToken)
    {
        var eventType = ResolveEventType(message);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var evt = DeserializeAndValidate(message);

            using var logScope = BeginProcessingLogScope(message, evt);
            using var activity = StartConsumerActivity(message, evt);
            var processingResult = await ProcessMessageAsync(message.EventType, evt, cancellationToken);
            RecordSuccessfulConsumerResult(message.Transport.Source, eventType, startedAt, processingResult);

            return true;
        }
        catch (JsonException ex)
        {
            return await PublishToDeadLetterAndAllowCommitAsync(
                message,
                "Deserialization failed.",
                ex,
                eventType,
                startedAt,
                cancellationToken);
        }
        catch (MessageValidationException ex)
        {
            return await PublishToDeadLetterAndAllowCommitAsync(
                message,
                ex.Message,
                ex,
                eventType,
                startedAt,
                cancellationToken);
        }
        catch (DomainException ex)
        {
            return await PublishToDeadLetterAndAllowCommitAsync(
                message,
                "Non-recoverable processing failure.",
                ex,
                eventType,
                startedAt,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await PublishToDeadLetterAndAllowCommitAsync(
                message,
                "Non-recoverable processing failure.",
                ex,
                eventType,
                startedAt,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsNonRecoverableProcessingFailure(ex))
        {
            return await PublishToDeadLetterAndAllowCommitAsync(
                message,
                "Non-recoverable processing failure.",
                ex,
                eventType,
                startedAt,
                cancellationToken);
        }
    }

    private static LedgerEntryCreatedEvent DeserializeAndValidate(ReceivedMessage message)
    {
        var contractVersion = LedgerEntryCreatedEventValidator.ValidateEventType(message);
        var deserialized = JsonSerializer.Deserialize<LedgerEntryCreatedEvent>(message.Payload, JsonOptions);
        return LedgerEntryCreatedEventValidator.Validate(deserialized, contractVersion);
    }

    private IDisposable? BeginProcessingLogScope(ReceivedMessage message, LedgerEntryCreatedEvent evt)
        => _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = evt.CorrelationId,
            ["EventId"] = ResolveEventId(message, evt.Id),
            ["MerchantId"] = evt.MerchantId,
            ["OccurredAt"] = evt.OccurredAt,
            ["TransportProvider"] = message.Transport.Provider,
            ["TransportSource"] = message.Transport.Source,
            ["EventType"] = message.EventType
        });

    private async Task<ApplyLedgerEntryCreatedResult> ProcessMessageAsync(
        string eventType,
        LedgerEntryCreatedEvent evt,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new ApplyLedgerEntryCreatedCommand(evt, eventType), ct);
    }

    private async Task PublishToDeadLetterAsync(
        ReceivedMessage receivedMessage,
        string reason,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = new DeadLetterMessage(
            receivedMessage.Payload,
            receivedMessage.Transport.Source,
            receivedMessage.Transport.Provider,
            ResolveEventType(receivedMessage),
            reason,
            exception.GetType().Name,
            DateTimeOffset.UtcNow,
            receivedMessage.Attributes,
            receivedMessage.Transport.Metadata);

        await _deadLetterPublisher.PublishAsync(message, cancellationToken);

        _logger.LogWarning(
            exception,
            "Mensagem enviada para DLQ. provider={Provider} source={Source} partition={Partition} offset={Offset} reason={Reason}",
            receivedMessage.Transport.Provider,
            receivedMessage.Transport.Source,
            receivedMessage.Transport.Partition,
            receivedMessage.Transport.Offset,
            reason);
    }

    private async Task<bool> PublishToDeadLetterAndAllowCommitAsync(
        ReceivedMessage message,
        string reason,
        Exception exception,
        string eventType,
        long startedAt,
        CancellationToken cancellationToken)
    {
        await PublishToDeadLetterAsync(message, reason, exception, cancellationToken);
        RecordDlqConsumerResult(message.Transport.Source, eventType, startedAt);
        return true;
    }

    private void RecordSuccessfulConsumerResult(
        string source,
        string eventType,
        long startedAt,
        ApplyLedgerEntryCreatedResult processingResult)
    {
        var metricResult = processingResult.Duplicate ? "duplicate" : "success";
        _metrics.RecordConsumerMessageConsumed(source, eventType, metricResult);
        _metrics.RecordConsumerProcessingDuration(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            source,
            eventType,
            metricResult);

        if (processingResult.Duplicate)
            _metrics.RecordConsumerDuplicate(source, eventType);
    }

    private void RecordDlqConsumerResult(string source, string eventType, long startedAt)
    {
        _metrics.RecordConsumerMessageConsumed(source, eventType, "dlq");
        _metrics.RecordConsumerProcessingDuration(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, source, eventType, "dlq");
    }

    private static Activity? StartConsumerActivity(ReceivedMessage message, LedgerEntryCreatedEvent evt)
    {
        var activity = StartActivity(
            "message.process",
            message.TraceParent,
            message.TraceState,
            message.Baggage);

        activity?.SetTag("messaging.system", message.Transport.Provider);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.destination", message.Transport.Source);
        activity?.SetTag("correlation_id", evt.CorrelationId);
        activity?.SetTag("event_id", ResolveEventId(message, evt.Id));
        activity?.SetTag("event_type", message.EventType);
        activity?.AddBaggage("correlation_id", evt.CorrelationId);

        return activity;
    }

    private static Activity? StartActivity(
        string operationName,
        string? traceParent,
        string? traceState,
        string? baggage)
    {
        Activity? activity = null;

        if (!string.IsNullOrWhiteSpace(traceParent) &&
            ActivityContext.TryParse(traceParent, traceState, out var parentContext))
        {
            activity = ActivitySource.StartActivity(operationName, ActivityKind.Consumer, parentContext);
        }

        activity ??= ActivitySource.StartActivity(operationName, ActivityKind.Consumer);

        if (activity is not null && !string.IsNullOrWhiteSpace(baggage))
        {
            foreach (var item in baggage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = item.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    activity.AddBaggage(parts[0], parts[1]);
            }
        }

        return activity;
    }

    private static string ResolveEventId(ReceivedMessage message, string payloadEventId)
        => !string.IsNullOrWhiteSpace(message.EventId)
            ? message.EventId
            : payloadEventId;

    private static string ResolveEventType(ReceivedMessage message)
        => !string.IsNullOrWhiteSpace(message.EventType)
            ? message.EventType
            : "unknown";

    private static bool IsNonRecoverableProcessingFailure(InvalidOperationException ex)
        => ex.Source?.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) != true;

    private static bool IsValidAmountForType(LedgerEntryCreatedEvent evt, out decimal amount)
    {
        amount = default;
        return AmountPattern().IsMatch(evt.Amount) &&
            decimal.TryParse(evt.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) &&
            amount != 0 &&
            evt.Type is "CREDIT" or "DEBIT" &&
            (evt.Type != "CREDIT" || amount >= 0) &&
            (evt.Type != "DEBIT" || amount <= 0);
    }

    [GeneratedRegex("^lan_[0-9a-f]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex EventIdPattern();

    [GeneratedRegex("^-?[0-9]+\\.[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();

    private enum LedgerEntryCreatedContractVersion
    {
        V1,
        V2
    }

    private static class LedgerEntryCreatedEventValidator
    {
        public static LedgerEntryCreatedContractVersion ValidateEventType(ReceivedMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.EventType))
                throw new MessageValidationException("Missing required message attribute event_type.");

            return message.EventType switch
            {
                LedgerEntryCreatedV1Contract.EventType => LedgerEntryCreatedContractVersion.V1,
                LedgerEntryCreatedV2Contract.EventType => LedgerEntryCreatedContractVersion.V2,
                _ => throw new MessageValidationException($"Unsupported message event_type '{message.EventType}'.")
            };
        }

        public static LedgerEntryCreatedEvent Validate(
            LedgerEntryCreatedEvent? evt,
            LedgerEntryCreatedContractVersion contractVersion)
        {
            if (evt is null)
                throw new MessageValidationException("Message payload is empty or invalid.");

            ValidateRequiredFields(evt);
            ValidateBusinessShape(evt);
            var currency = LedgerEntryCreatedEventNormalizer.NormalizeCurrency(evt, contractVersion);

            return evt with
            {
                Currency = currency
            };
        }

        private static void ValidateRequiredFields(LedgerEntryCreatedEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt.Id))
                throw new MessageValidationException("Message payload id is required.");

            if (string.IsNullOrWhiteSpace(evt.Type))
                throw new MessageValidationException("Message payload type is required.");

            if (string.IsNullOrWhiteSpace(evt.Amount))
                throw new MessageValidationException("Message payload amount is required.");

            if (evt.CreatedAt == default)
                throw new MessageValidationException("Message payload createdAt is required.");

            if (string.IsNullOrWhiteSpace(evt.MerchantId))
                throw new MessageValidationException("Message payload merchantId is required.");

            if (evt.OccurredAt == default)
                throw new MessageValidationException("Message payload occurredAt is required.");

            if (string.IsNullOrWhiteSpace(evt.CorrelationId))
                throw new MessageValidationException("Message payload correlationId is required.");
        }

        private static void ValidateBusinessShape(LedgerEntryCreatedEvent evt)
        {
            if (!EventIdPattern().IsMatch(evt.Id))
                throw new MessageValidationException("Message payload id is invalid.");

            if (!IsValidAmountForType(evt, out _))
                throw new MessageValidationException("Message payload type and amount are invalid.");

            if (!Guid.TryParse(evt.CorrelationId, out _))
                throw new MessageValidationException("Message payload correlationId must be a UUID.");
        }
    }

    private static class LedgerEntryCreatedEventNormalizer
    {
        public static string NormalizeCurrency(
            LedgerEntryCreatedEvent evt,
            LedgerEntryCreatedContractVersion contractVersion)
        {
            var currency = ResolveCurrency(evt, contractVersion);

            if (string.IsNullOrWhiteSpace(currency))
                throw new MessageValidationException("Message payload currency is required.");

            currency = currency.Trim().ToUpperInvariant();

            if (!CurrencyPattern().IsMatch(currency))
                throw new MessageValidationException("Message payload currency is invalid.");

            return currency;
        }

        private static string? ResolveCurrency(
            LedgerEntryCreatedEvent evt,
            LedgerEntryCreatedContractVersion contractVersion)
        {
            if (contractVersion == LedgerEntryCreatedContractVersion.V1)
            {
                if (evt.Currency is not null)
                    throw new MessageValidationException("Message payload currency is not supported in LedgerEntryCreated.v1.");

                return LegacyV1CurrencyFallback;
            }

            return evt.Currency;
        }
    }

    private sealed class MessageValidationException : Exception
    {
        public MessageValidationException(string message) : base(message)
        {
        }
    }
}
