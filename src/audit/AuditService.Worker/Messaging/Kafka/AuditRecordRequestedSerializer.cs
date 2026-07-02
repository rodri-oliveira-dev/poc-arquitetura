using System.Text.Json;

using AuditService.Worker.Messaging.Kafka.Contracts;

namespace AuditService.Worker.Messaging.Kafka;

internal static class AuditRecordRequestedSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AuditRecordRequestedEvent Deserialize(string json)
        => string.IsNullOrWhiteSpace(json)
            ? throw new JsonException("AuditRecordRequested.v1 JSON is required.")
            : JsonSerializer.Deserialize<AuditRecordRequestedEvent>(json, JsonOptions)
                ?? throw new JsonException("AuditRecordRequested.v1 JSON is invalid.");
}
