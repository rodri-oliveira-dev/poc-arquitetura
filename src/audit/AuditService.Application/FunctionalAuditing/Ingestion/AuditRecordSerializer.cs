using System.Text.Json;

namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed class AuditRecordSerializer : IAuditRecordSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Serialize(AuditRecordEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public AuditRecordEnvelope Deserialize(string json)
        => string.IsNullOrWhiteSpace(json)
            ? throw new JsonException("Audit record envelope JSON is required.")
            : JsonSerializer.Deserialize<AuditRecordEnvelope>(json, JsonOptions)
                ?? throw new JsonException("Audit record envelope JSON is invalid.");
}
