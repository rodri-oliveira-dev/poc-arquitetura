using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityService.Api.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<AllowAnonymousAttribute>().Any())
        {
            return;
        }

        var authorizeData = metadata.OfType<IAuthorizeData>().ToArray();
        if (authorizeData.Length == 0)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        var requiredScopes = authorizeData
            .Select(data => data.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Select(MapPolicyToScope)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requiredScopes.Length == 0)
        {
            return;
        }

        operation.Description = string.Join("\n\n", new[]
        {
            operation.Description,
            $"<b>Autorizacao</b>: requer scope(s): <c>{string.Join(" ", requiredScopes)}</c>. (claim <c>scope</c> do JWT)"
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? MapPolicyToScope(string? policy)
        => policy is not null && policy.StartsWith("scope:", StringComparison.Ordinal)
            ? policy["scope:".Length..]
            : null;
}
