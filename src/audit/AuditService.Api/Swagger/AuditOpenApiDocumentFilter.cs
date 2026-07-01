using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuditService.Api.Swagger;

public sealed class AuditOpenApiDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);

        if (swaggerDoc.Tags is not null)
        {
            foreach (OpenApiTag tag in swaggerDoc.Tags)
            {
                if (string.Equals(tag.Name, "AuditRecords", StringComparison.Ordinal))
                    tag.Description = "Operacoes para criacao e consulta de registros canonicos de auditoria funcional.";
            }
        }

        MarkAuditRecordEndpointsAsPublic(swaggerDoc);
    }

    private static void MarkAuditRecordEndpointsAsPublic(OpenApiDocument swaggerDoc)
    {
        if (swaggerDoc.Paths is null)
            return;

        foreach (KeyValuePair<string, IOpenApiPathItem> item in swaggerDoc.Paths)
        {
            if (!item.Key.StartsWith("/api/v1/audit-records", StringComparison.Ordinal) ||
                item.Value.Operations is null)
            {
                continue;
            }

            foreach (OpenApiOperation operation in item.Value.Operations.Values)
                operation.Security = [];
        }
    }
}
