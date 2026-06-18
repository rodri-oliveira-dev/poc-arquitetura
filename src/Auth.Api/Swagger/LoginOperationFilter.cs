using Auth.Api.Security;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System.Text.Json.Nodes;

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
            reqMedia!.Example = new JsonObject
            {
                ["username"] = "<configure Auth:DevelopmentUser:Username>",
                ["password"] = "<configure Auth:DevelopmentUser:Password>",
                ["scope"] = "ledger.write balance.read"
            };
        }

        // Response examples
        if (operation.Responses.TryGetValue("200", out var r200) && r200.Content.TryGetValue("application/json", out var m200))
        {
            m200!.Example = new JsonObject
            {
                ["access_token"] = "<jwt>",
                ["token_type"] = "Bearer",
                ["expires_in"] = 600,
                ["scope"] = "ledger.write balance.read"
            };
        }

        if (operation.Responses.TryGetValue("401", out var r401) && r401.Content.TryGetValue("application/json", out var m401))
        {
            m401!.Example = new JsonObject
            {
                ["error"] = "invalid_credentials",
                ["message"] = "Usuário ou senha inválidos."
            };
        }

        if (operation.Responses.TryGetValue("400", out var r400) && r400.Content.TryGetValue("application/json", out var m400))
        {
            m400!.Example = new JsonObject
            {
                ["error"] = "invalid_scope",
                ["message"] = $"Informe ao menos um scope explicito. Scopes validos: {ScopeCatalog.ValidScopesAsString()}"
            };
        }

        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-valid-scopes"] = new JsonNodeExtension(JsonValue.Create(ScopeCatalog.ValidScopesAsString())!);
    }
}
