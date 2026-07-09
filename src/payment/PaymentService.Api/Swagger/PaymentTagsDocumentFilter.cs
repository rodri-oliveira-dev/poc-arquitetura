using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentService.Api.Swagger;

public sealed class PaymentTagsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);

        if (swaggerDoc.Tags is null)
            return;

        foreach (var tag in swaggerDoc.Tags.Where(tag => string.Equals(tag.Name, "Payments", StringComparison.Ordinal)))
        {
            tag.Description = "Endpoints de criacao local e consulta de Payments.";
        }
    }
}
