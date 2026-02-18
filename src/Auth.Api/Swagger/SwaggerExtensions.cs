using System.Reflection;

using Auth.Api.Middlewares;

using Microsoft.OpenApi.Models;

namespace Auth.Api.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Auth.Api",
                Version = "v1",
                Description = "Microserviço Minimal API responsável por emitir JWT (RS256) e expor JWKS para validação offline."
            });

            options.OperationFilter<LoginOperationFilter>();

            // XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);

            // Bearer
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe: Bearer {token}"
            });

            // Correlation id (padrão do repo)
            options.AddSecurityDefinition(CorrelationIdMiddleware.HeaderName, new OpenApiSecurityScheme
            {
                Name = CorrelationIdMiddleware.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Identificador de correlação (UUID). Se não for enviado, a API gera um novo e o retorna no header de response."
            });
        });

        return services;
    }
}
