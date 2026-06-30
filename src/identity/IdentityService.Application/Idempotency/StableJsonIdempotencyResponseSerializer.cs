using System.Text;
using System.Text.Json;

namespace IdentityService.Application.Idempotency;

public sealed class StableJsonIdempotencyResponseSerializer : IIdempotencyResponseSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public string Serialize<TResponse>(TResponse response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        using var document = JsonDocument.Parse(json);

        return Canonicalize(document.RootElement);
    }

    public TResponse Deserialize<TResponse>(string responseBody)
    {
        return string.IsNullOrWhiteSpace(responseBody)
            ? throw new ArgumentException("Response body is required.", nameof(responseBody))
            : JsonSerializer.Deserialize<TResponse>(responseBody, _jsonOptions)
            ?? throw new InvalidOperationException("Persisted idempotency response body could not be deserialized.");
    }

    private static string Canonicalize(JsonElement element)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalElement(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                element.WriteTo(writer);
                break;

            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }
}
