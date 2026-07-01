using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuditService.Api.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<AllowAnonymousAttribute>().Any())
            return;

        var authorizeData = metadata.OfType<IAuthorizeData>().ToArray();
        if (authorizeData.Length == 0)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        string[] policies = [.. authorizeData
            .Select(static data => data.Policy)
            .Where(static policy => !string.IsNullOrWhiteSpace(policy))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)];

        if (policies.Length == 0)
            return;

        operation.Description = string.Join("\n\n", new[]
        {
            operation.Description,
            $"<b>Autorizacao</b>: requer policy/scope <c>{string.Join(" ou ", policies)}</c>. (claim <c>scope</c> do JWT)"
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }
}
