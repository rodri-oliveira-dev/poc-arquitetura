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

    }
}
