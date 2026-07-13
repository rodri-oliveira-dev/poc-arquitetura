using ApiDefaults.Extensions;

using Microsoft.OpenApi;

using PaymentService.Api.Observability;
using PaymentService.Api.Security;
using PaymentService.Api.Swagger;

namespace PaymentService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
                    Description = $"Autenticacao via JWT Bearer. Scopes relevantes nesta API: {ScopePolicies.PaymentWrite} (escrita) / {ScopePolicies.PaymentRead} (leitura) / {ScopePolicies.PaymentRefund} (refund)."
                });
                options.OperationFilter<AuthorizeOperationFilter>();
                options.OperationFilter<StripeWebhookOperationFilter>();
                options.DocumentFilter<PaymentTagsDocumentFilter>();
            });
    }

    public static IServiceCollection AddApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddConfiguredApiOpenTelemetryDefaults<OpenTelemetryOptions>(
            configuration,
            OpenTelemetryOptions.SectionName,
            options => options.Enabled,
            options => options.ServiceName,
            options => options.UseConsoleExporter,
            options => options.OtlpEndpoint,
            tracing => tracing
                .AddSource("PaymentService.Api")
                .AddSource("PaymentService.Application")
                .AddSource("PaymentService.Infrastructure.PaymentGateway"));
    }
}
