using System.Text.Json;

using Json.Schema;

namespace BalanceService.Application.Contracts.Events;

public sealed class JsonSchemaEventContractValidator : IEventContractValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List
    };

    private readonly IEventContractSchemaCatalog _schemaCatalog;

    public JsonSchemaEventContractValidator(IEventContractSchemaCatalog schemaCatalog)
    {
        _schemaCatalog = schemaCatalog;
    }

    public EventContractValidationResult Validate(EventContractValidationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (string.IsNullOrWhiteSpace(candidate.EventName))
        {
            return EventContractValidationResult.Failure(
                EventContractValidationErrorCode.EventNameMissing,
                "EventName is required.",
                candidate.EventName,
                candidate.EventVersion);
        }

        if (string.IsNullOrWhiteSpace(candidate.EventVersion))
        {
            return EventContractValidationResult.Failure(
                EventContractValidationErrorCode.EventVersionMissing,
                "EventVersion is required.",
                candidate.EventName,
                candidate.EventVersion);
        }

        string eventName = candidate.EventName.Trim();
        string eventVersion = candidate.EventVersion.Trim();

        if (!_schemaCatalog.ContainsEventName(eventName))
        {
            return EventContractValidationResult.Failure(
                EventContractValidationErrorCode.SchemaNotFound,
                $"No event contract schema was found for '{eventName}'.",
                eventName,
                eventVersion);
        }

        if (!_schemaCatalog.TryGetSchema(eventName, eventVersion, out JsonSchema? schema) || schema is null)
        {
            return EventContractValidationResult.Failure(
                EventContractValidationErrorCode.UnsupportedVersion,
                $"Event contract version '{eventVersion}' is not supported for '{eventName}'.",
                eventName,
                eventVersion);
        }

        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(candidate.Payload);
        }
        catch (JsonException ex)
        {
            return EventContractValidationResult.Failure(
                EventContractValidationErrorCode.InvalidJson,
                $"Payload is not valid JSON. {ex.Message}",
                eventName,
                eventVersion);
        }

        using (payload)
        {
            EvaluationResults result = schema.Evaluate(payload.RootElement, EvaluationOptions);
            if (result.IsValid)
                return EventContractValidationResult.Success(eventName, eventVersion);

            return EventContractValidationResult.Failure(
                EventContractValidationErrorCode.InvalidPayload,
                JsonSerializer.Serialize(result, JsonOptions),
                eventName,
                eventVersion);
        }
    }
}
