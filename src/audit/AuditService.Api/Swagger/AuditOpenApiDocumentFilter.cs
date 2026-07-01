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
                    tag.Description = "Operacoes para criacao de registros canonicos de auditoria funcional.";
            }
        }

        MarkAuditRecordCreationAsPublic(swaggerDoc);
    }

    private static void MarkAuditRecordCreationAsPublic(OpenApiDocument swaggerDoc)
    {
        if (swaggerDoc.Paths is null ||
            !swaggerDoc.Paths.TryGetValue("/api/v1/audit-records", out IOpenApiPathItem? pathItem) ||
            pathItem.Operations is null ||
            !pathItem.Operations.TryGetValue(HttpMethod.Post, out OpenApiOperation? operation))
        {
            return;
        }

        operation.Security = [];
    }
}
