using System.Text.Json;

using AuditService.Worker.Messaging.Kafka.Contracts;

using FluentValidation;

using MediatR;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed partial class AuditRecordRequestedProcessor(
    AuditRecordRequestedValidator validator,
    ISender sender,
    ILogger<AuditRecordRequestedProcessor> logger) : IAuditRecordRequestedProcessor
{
    public async Task<bool> ProcessAsync(string messageValue, CancellationToken cancellationToken)
    {
        AuditRecordRequestedEvent message;
        try
        {
            message = AuditRecordRequestedSerializer.Deserialize(messageValue);
        }
        catch (JsonException ex)
        {
            LogInvalidAuditRecordRequestedJson(logger, ex);
            return true;
        }

        try
        {
            validator.ValidateAndThrow(message);

            await sender.Send(AuditRecordRequestedMapper.Map(message), cancellationToken);

            LogAuditRecordRequestedProcessed(
                logger,
                message.EventId,
                message.CorrelationId,
                message.OperationId,
                message.SourceService,
                message.OperationType);

            return true;
        }
        catch (ValidationException ex)
        {
            LogInvalidAuditRecordRequestedContract(logger, ex, message.EventId, message.CorrelationId);
            return true;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "AuditRecordRequested.v1 processado. eventId={EventId} correlationId={CorrelationId} operationId={OperationId} sourceService={SourceService} operationType={OperationType}")]
    private static partial void LogAuditRecordRequestedProcessed(
        ILogger logger,
        Guid eventId,
        Guid? correlationId,
        Guid operationId,
        string sourceService,
        string operationType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 invalido: JSON nao pode ser desserializado.")]
    private static partial void LogInvalidAuditRecordRequestedJson(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 invalido: contrato rejeitado. eventId={EventId} correlationId={CorrelationId}")]
    private static partial void LogInvalidAuditRecordRequestedContract(
        ILogger logger,
        Exception exception,
        Guid eventId,
        Guid? correlationId);
}
