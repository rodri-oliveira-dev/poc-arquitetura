using Auth.Api.Security;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Auth.Api.Swagger;

/// <summary>
/// Enriquecimento do Swagger para o endpoint /auth/login:
/// - adiciona exemplos de request/response
/// - documenta scopes válidos
/// </summary>
public sealed class LoginOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + context.ApiDescription.RelativePath?.TrimStart('/');
        if (!string.Equals(path, "/auth/login", StringComparison.OrdinalIgnoreCase))
            return;

        // Request example
        if (operation.RequestBody?.Content.TryGetValue("application/json", out var reqMedia) == true)
        {
            reqMedia.Example = new OpenApiObject
            {
                ["username"] = new OpenApiString("poc-usuario"),
                ["password"] = new OpenApiString("Poc#123"),
                ["scope"] = new OpenApiString("ledger.write balance.read")
            };
        }

        // Response examples
        if (operation.Responses.TryGetValue("200", out var r200) && r200.Content.TryGetValue("application/json", out var m200))
        {
            m200.Example = new OpenApiObject
            {
                ["access_token"] = new OpenApiString("<jwt>"),
                ["token_type"] = new OpenApiString("Bearer"),
                ["expires_in"] = new OpenApiInteger(600),
                ["scope"] = new OpenApiString("ledger.write balance.read")
            };
        }

        if (operation.Responses.TryGetValue("401", out var r401) && r401.Content.TryGetValue("application/json", out var m401))
        {
            m401.Example = new OpenApiObject
            {
                ["error"] = new OpenApiString("invalid_credentials"),
                ["message"] = new OpenApiString("Usuário ou senha inválidos.")
            };
        }

        if (operation.Responses.TryGetValue("400", out var r400) && r400.Content.TryGetValue("application/json", out var m400))
        {
            m400.Example = new OpenApiObject
            {
                ["error"] = new OpenApiString("invalid_scope"),
                ["message"] = new OpenApiString($"Scopes inválidos: x y. Scopes válidos: {ScopeCatalog.ValidScopesAsString()}")
            };
        }

        operation.Extensions["x-valid-scopes"] = new OpenApiString(ScopeCatalog.ValidScopesAsString());
    }
}
