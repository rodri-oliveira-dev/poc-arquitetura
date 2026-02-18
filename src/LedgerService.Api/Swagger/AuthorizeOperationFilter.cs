using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LedgerService.Api.Swagger;

/// <summary>
/// Enriquecimento do Swagger para endpoints com <see cref="AuthorizeAttribute"/>.
/// - Adiciona requirement do esquema Bearer para o endpoint.
/// - Acrescenta no description quais scopes (policies) são exigidos.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Se AllowAnonymous estiver presente, não aplicar autenticação.
        var hasAllowAnonymous = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AllowAnonymousAttribute>()
            .Any();

        if (hasAllowAnonymous)
            return;

        var authorizeAttributes = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>() ?? [])
            .ToArray();

        if (authorizeAttributes.Length == 0)
            return;

        // Security requirement (para aparecer o cadeado e permitir "Authorize" no UI)
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = []
        });

        // Documenta policy/scope esperado
        var policies = authorizeAttributes
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (policies.Length == 0)
            return;

        var requiredScopes = policies
            .Select(p => MapPolicyToScope(p!))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requiredScopes.Length == 0)
            return;

        operation.Description = string.Join("\n\n", new[]
        {
            operation.Description,
            $"<b>Autorização</b>: requer scope(s): <c>{string.Join(" ", requiredScopes)}</c>. (claim <c>scope</c> do JWT)"
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? MapPolicyToScope(string policy)
    {
        // Padrão adotado no projeto: "scope:{scopeName}"
        if (policy.StartsWith("scope:", StringComparison.Ordinal))
            return policy["scope:".Length..];

        return null;
    }
}