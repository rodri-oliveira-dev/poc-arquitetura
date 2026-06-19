using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiDefaults.Swagger;

public sealed class OpenApiContractQualityDocumentFilter : IDocumentFilter
{
    private static readonly Dictionary<string, string> TagDescriptions = new(StringComparer.Ordinal)
    {
        ["Lancamentos"] = "Operacoes de escrita, estorno, reprocessamento e consulta de status do ledger.",
        ["OutboxAdmin"] = "Operacoes administrativas protegidas para inspecao e recuperacao da DLQ do Outbox.",
        ["Consolidados"] = "Consultas protegidas de saldo consolidado diario e por periodo.",
        ["Transferencias"] = "Operacoes protegidas para solicitar transferencias e consultar o status da saga.",
        ["Operacional"] = "Endpoints operacionais publicos de liveness e readiness."
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);

        if (swaggerDoc.Tags is null)
        {
            return;
        }

        foreach (OpenApiTag tag in swaggerDoc.Tags)
        {
            if (tag.Name is not null && TagDescriptions.TryGetValue(tag.Name, out string? description))
            {
                tag.Description = description;
            }
        }

        MarkPublicOperation(swaggerDoc, "/health");
        MarkPublicOperation(swaggerDoc, "/ready");
    }

    private static void MarkPublicOperation(OpenApiDocument swaggerDoc, string path)
    {
        if (swaggerDoc.Paths is null ||
            !swaggerDoc.Paths.TryGetValue(path, out IOpenApiPathItem? pathItem) ||
            pathItem.Operations is null ||
            !pathItem.Operations.TryGetValue(HttpMethod.Get, out OpenApiOperation? operation))
        {
            return;
        }

        operation.Security = [];
    }
}
