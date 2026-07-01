using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityService.Api.Swagger;

public sealed class CreateUserIdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!string.Equals(operation.OperationId, "CreateIdentityUser", StringComparison.Ordinal))
            return;

        operation.Parameters ??= [];

        if (operation.Parameters.Any(parameter =>
            string.Equals(parameter.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = 128,
                Pattern = "^[A-Za-z0-9._:-]{1,128}$"
            },
            Description = "Chave opcional de idempotencia para replay seguro do cadastro. Formato: ^[A-Za-z0-9._:-]{1,128}$."
        });
    }
}
