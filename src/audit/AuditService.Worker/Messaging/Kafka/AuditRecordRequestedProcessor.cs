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
    ILogger<AuditRecordRequestedProcessor> logger) : IAuditRecordRequestedProcessor
{
    public async Task<AuditRecordRequestedProcessingResult> ProcessAsync(
        AuditKafkaReceivedMessage receivedMessage,
        CancellationToken cancellationToken)
    {
        AuditRecordRequestedEvent message;
        try
        {
            message = AuditRecordRequestedSerializer.Deserialize(receivedMessage.Payload);
        }
        catch (JsonException ex)
        {
            await PublishToDeadLetterAsync(
                receivedMessage,
                "JSON invalido.",
                "invalid_json",
                ex,
                eventId: null,
                correlationId: null,
                cancellationToken);

            LogInvalidAuditRecordRequestedJson(logger, ex, receivedMessage.Topic, receivedMessage.Partition, receivedMessage.Offset);
            return AuditRecordRequestedProcessingResult.DeadLetter;
        }

        try
        {
            validator.ValidateAndThrow(message);

            var result = await sender.Send(AuditRecordRequestedMapper.Map(message), cancellationToken);

            if (result.Duplicate)
            {
                LogAuditRecordRequestedDuplicated(
                    logger,
                    message.EventId,
                    message.CorrelationId,
                    message.OperationId,
                    message.SourceService,
                    message.OperationType);

                return AuditRecordRequestedProcessingResult.Duplicate;
            }

            LogAuditRecordRequestedProcessed(
                logger,
                message.EventId,
                message.CorrelationId,
                message.OperationId,
                message.SourceService,
                message.OperationType);

            return AuditRecordRequestedProcessingResult.Success;
        }
        catch (ValidationException ex)
        {
            await PublishToDeadLetterAsync(
                receivedMessage,
                "Contrato invalido.",
                "invalid_contract",
                ex,
                message.EventId,
                message.CorrelationId,
                cancellationToken);

            LogInvalidAuditRecordRequestedContract(logger, ex, message.EventId, message.CorrelationId);
            return AuditRecordRequestedProcessingResult.DeadLetter;
        }
        catch (ConflictException ex)
        {
            await PublishToDeadLetterAsync(
                receivedMessage,
                "Conflito de idempotencia.",
                "idempotency_conflict",
                ex,
                message.EventId,
                message.CorrelationId,
                cancellationToken);

            LogAuditRecordRequestedIdempotencyConflict(logger, ex, message.EventId, message.CorrelationId);
            return AuditRecordRequestedProcessingResult.DeadLetter;
        }
    }

    private async Task PublishToDeadLetterAsync(
        AuditKafkaReceivedMessage receivedMessage,
        string failureReason,
        string failureCategory,
        Exception exception,
        Guid? eventId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        var deadLetterMessage = new AuditRecordDeadLetterMessage(
            eventId,
            correlationId,
            receivedMessage.Topic,
            receivedMessage.Partition,
            receivedMessage.Offset,
            failureReason,
            failureCategory,
            DateTimeOffset.UtcNow,
            ComputeSha256(receivedMessage.Payload));

        await deadLetterPublisher.PublishAsync(deadLetterMessage, cancellationToken);

        LogAuditRecordRequestedSentToDlq(
            logger,
            exception,
            receivedMessage.Topic,
            receivedMessage.Partition,
            receivedMessage.Offset,
            eventId,
            correlationId,
            failureCategory);
    }

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

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 enviado para DLQ. topic={Topic} partition={Partition} offset={Offset} eventId={EventId} correlationId={CorrelationId} failureCategory={FailureCategory}")]
    private static partial void LogAuditRecordRequestedSentToDlq(
        ILogger logger,
        Exception exception,
        string topic,
        int partition,
        long offset,
        Guid? eventId,
        Guid? correlationId,
        string failureCategory);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 com conflito definitivo de idempotencia. eventId={EventId} correlationId={CorrelationId}")]
    private static partial void LogAuditRecordRequestedIdempotencyConflict(
        ILogger logger,
        Exception exception,
        Guid eventId,
        Guid? correlationId);
}
