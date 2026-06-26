using ApiDefaults.Authentication;
using ApiDefaults.Extensions;

using IdentityService.Api.Middlewares;
using IdentityService.Api.Security;
using IdentityService.Api.Swagger;
using IdentityService.Application.Users.Commands;

using Microsoft.OpenApi;

namespace IdentityService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApiComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddApiDefaults<GlobalExceptionHandler>(configuration, "identity.localhost", "localhost")
            .AddIdentityApiSwagger();

        services.AddApiJwtBearerAuthentication(
            ReadJwtOptions(configuration, "Jwt"),
            environment,
            options => options.RequireAuthenticatedUserByDefault().AddScopePolicies());

        services.AddScoped<CreateUserCommandHandler>();
        services.AddEndpointsApiExplorer();

        return services;
    }

    private static IServiceCollection AddIdentityApiSwagger(this IServiceCollection services)
    {
        return services.AddApiSwaggerDefaults<ConfigureSwaggerOptions>(
            typeof(Program).Assembly,
            options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = $"Autenticacao via JWT Bearer. Audience esperada: identity-api. Scopes: {ScopePolicies.IdentityWrite} (cadastro) / {ScopePolicies.IdentityRead} (consulta futura)."
                });
                options.OperationFilter<AuthorizeOperationFilter>();
            });
    }

    private static ApiJwtAuthenticationOptions ReadJwtOptions(IConfiguration configuration, string sectionName)
        => new(
            sectionName,
            configuration.GetValue<string>($"{sectionName}:Issuer") ?? string.Empty,
            configuration.GetValue<string>($"{sectionName}:Audience") ?? string.Empty,
            configuration.GetValue<string>($"{sectionName}:JwksUrl") ?? string.Empty,
            configuration.GetValue($"{sectionName}:RequireHttpsMetadata", true),
            configuration.GetValue($"{sectionName}:JwksTimeoutSeconds", 5),
            configuration.GetValue($"{sectionName}:JwksRetryCount", 2),
            configuration.GetValue($"{sectionName}:JwksRetryBaseDelayMilliseconds", 200));
}
