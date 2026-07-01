using System.Reflection;

using Json.Schema;

namespace BalanceService.Application.Contracts.Events;

public sealed class EmbeddedEventContractSchemaCatalog : IEventContractSchemaCatalog
{
    private static readonly IReadOnlyCollection<EventContractSchemaDescriptor> Descriptors =
    [
        new("LedgerEntryCreated", "v1", "ledger-entry-created.v1.schema.json"),
        new("LedgerEntryCreated", "v2", "ledger-entry-created.v2.schema.json"),
        new("LancamentoEstornoSolicitado", "v1", "lancamento-estorno-solicitado.v1.schema.json"),
        new("ReprocessamentoLancamentosSolicitado", "v1", "reprocessamento-lancamentos-solicitado.v1.schema.json")
    ];

    private static readonly Assembly Assembly = typeof(EmbeddedEventContractSchemaCatalog).Assembly;
    private static readonly Lazy<Dictionary<EventContractKey, JsonSchema>> Schemas = new(LoadSchemas);

    public bool ContainsEventName(string eventName)
        => Descriptors.Any(x => string.Equals(x.EventName, eventName, StringComparison.Ordinal));

    public bool TryGetSchema(string eventName, string eventVersion, out JsonSchema? schema)
        => Schemas.Value.TryGetValue(new EventContractKey(eventName, eventVersion), out schema);

    private static Dictionary<EventContractKey, JsonSchema> LoadSchemas()
    {
        var schemas = new Dictionary<EventContractKey, JsonSchema>();

        foreach (EventContractSchemaDescriptor descriptor in Descriptors)
        {
            string resourceName = ResolveResourceName(descriptor.ResourceFileName);
            using Stream stream = Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded event schema resource '{descriptor.ResourceFileName}' was not found.");
            using var reader = new StreamReader(stream);

            schemas[new EventContractKey(descriptor.EventName, descriptor.EventVersion)] =
                JsonSchema.FromText(reader.ReadToEnd());
        }

        return schemas;
    }

    private static string ResolveResourceName(string resourceFileName)
    {
        string? resourceName = Assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith(resourceFileName, StringComparison.Ordinal));

        return resourceName
            ?? throw new InvalidOperationException($"Embedded event schema resource '{resourceFileName}' was not found.");
    }

    private sealed record EventContractSchemaDescriptor(
        string EventName,
        string EventVersion,
        string ResourceFileName);
}
