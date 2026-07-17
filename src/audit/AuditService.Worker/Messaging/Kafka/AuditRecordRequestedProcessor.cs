using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AuditService.Application.Common.Exceptions;
using AuditService.Worker.Messaging.Kafka.Contracts;
using AuditService.Worker.Messaging.Kafka.DeadLetter;

using FluentValidation;

using MediatR;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed partial class AuditRecordRequestedProcessor(
    AuditRecordRequestedValidator validator,
    IAuditRecordDeadLetterPublisher deadLetterPublisher,
    ISender sender,
    TimeProvider timeProvider,
    ILogger<AuditRecordRequestedProcessor> logger) : IAuditRecordRequestedProcessor
{
    public async Task<AuditRecordRequestedProcessingResult> ProcessAsync(
        AuditKafkaReceivedMessage message,
        CancellationToken cancellationToken)
    {
        AuditRecordRequestedEvent requestedEvent;
        try
        {
            requestedEvent = AuditRecordRequestedSerializer.Deserialize(message.Payload);
        }
        catch (JsonException ex)
        {
            await PublishToDeadLetterAsync(
                new AuditRecordFailureContext(
                    message,
                    EventId: null,
                    CorrelationId: null,
                    FailureReason: "JSON invalido.",
                    FailureCategory: "invalid_json"),
                ex,
                cancellationToken);

            LogInvalidAuditRecordRequestedJson(logger, ex, message.Topic, message.Partition, message.Offset);
            return AuditRecordRequestedProcessingResult.DeadLetter;
        }

        try
        {
            await validator.ValidateAndThrowAsync(requestedEvent, cancellationToken);

            var result = await sender.Send(AuditRecordRequestedMapper.Map(requestedEvent), cancellationToken);

            if (result.Duplicate)
            {
                LogAuditRecordRequestedDuplicated(
                    logger,
                    requestedEvent.EventId,
                    requestedEvent.CorrelationId,
                    requestedEvent.OperationId,
                    requestedEvent.SourceService,
                    requestedEvent.OperationType);

                return AuditRecordRequestedProcessingResult.Duplicate;
            }

            LogAuditRecordRequestedProcessed(
                logger,
                requestedEvent.EventId,
                requestedEvent.CorrelationId,
                requestedEvent.OperationId,
                requestedEvent.SourceService,
                requestedEvent.OperationType);

            return AuditRecordRequestedProcessingResult.Success;
        }
        catch (ValidationException ex)
        {
            await PublishToDeadLetterAsync(
                new AuditRecordFailureContext(
                    message,
                    requestedEvent.EventId,
                    requestedEvent.CorrelationId,
                    "Contrato invalido.",
                    "invalid_contract"),
                ex,
                cancellationToken);

            LogInvalidAuditRecordRequestedContract(logger, ex, requestedEvent.EventId, requestedEvent.CorrelationId);
            return AuditRecordRequestedProcessingResult.DeadLetter;
        }
        catch (ConflictException ex)
        {
            await PublishToDeadLetterAsync(
                new AuditRecordFailureContext(
                    message,
                    requestedEvent.EventId,
                    requestedEvent.CorrelationId,
                    "Conflito de idempotencia.",
                    "idempotency_conflict"),
                ex,
                cancellationToken);

            LogAuditRecordRequestedIdempotencyConflict(logger, ex, requestedEvent.EventId, requestedEvent.CorrelationId);
            return AuditRecordRequestedProcessingResult.DeadLetter;
        }
    }

    private async Task PublishToDeadLetterAsync(
        AuditRecordFailureContext failure,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var deadLetterMessage = new AuditRecordDeadLetterMessage(
            failure.EventId,
            failure.CorrelationId,
            failure.Message.Topic,
            failure.Message.Partition,
            failure.Message.Offset,
            failure.FailureReason,
            failure.FailureCategory,
            timeProvider.GetUtcNow(),
            ComputeSha256(failure.Message.Payload));

        await deadLetterPublisher.PublishAsync(deadLetterMessage, cancellationToken);

        LogAuditRecordRequestedSentToDlq(
            logger,
            exception,
            failure);
    }

    private sealed record AuditRecordFailureContext(
        AuditKafkaReceivedMessage Message,
        Guid? EventId,
        Guid? CorrelationId,
        string FailureReason,
        string FailureCategory);

    private static string ComputeSha256(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "AuditRecordRequested.v1 processado. eventId={EventId} correlationId={CorrelationId} operationId={OperationId} sourceService={SourceService} operationType={OperationType}")]
    private static partial void LogAuditRecordRequestedProcessed(
        ILogger logger,
        Guid eventId,
        Guid? correlationId,
        Guid operationId,
        string sourceService,
        string operationType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 invalido: JSON nao pode ser desserializado. topic={Topic} partition={Partition} offset={Offset}")]
    private static partial void LogInvalidAuditRecordRequestedJson(
        ILogger logger,
        Exception exception,
        string topic,
        int partition,
        long offset);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 invalido: contrato rejeitado. eventId={EventId} correlationId={CorrelationId}")]
    private static partial void LogInvalidAuditRecordRequestedContract(
        ILogger logger,
        Exception exception,
        Guid eventId,
        Guid? correlationId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "AuditRecordRequested.v1 duplicado ignorado por idempotencia. eventId={EventId} correlationId={CorrelationId} operationId={OperationId} sourceService={SourceService} operationType={OperationType}")]
    private static partial void LogAuditRecordRequestedDuplicated(
        ILogger logger,
        Guid eventId,
        Guid? correlationId,
        Guid operationId,
        string sourceService,
        string operationType);

    private static readonly Action<ILogger, string, int, long, Guid?, Guid?, string, Exception?> AuditRecordRequestedSentToDlq =
        LoggerMessage.Define<string, int, long, Guid?, Guid?, string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogAuditRecordRequestedSentToDlq)),
            "AuditRecordRequested.v1 enviado para DLQ. topic={Topic} partition={Partition} offset={Offset} eventId={EventId} correlationId={CorrelationId} failureCategory={FailureCategory}");

    private static void LogAuditRecordRequestedSentToDlq(
        ILogger logger,
        Exception exception,
        AuditRecordFailureContext failure)
        => AuditRecordRequestedSentToDlq(
            logger,
            failure.Message.Topic,
            failure.Message.Partition,
            failure.Message.Offset,
            failure.EventId,
            failure.CorrelationId,
            failure.FailureCategory,
            exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 com conflito definitivo de idempotencia. eventId={EventId} correlationId={CorrelationId}")]
    private static partial void LogAuditRecordRequestedIdempotencyConflict(
        ILogger logger,
        Exception exception,
        Guid eventId,
        Guid? correlationId);
}
