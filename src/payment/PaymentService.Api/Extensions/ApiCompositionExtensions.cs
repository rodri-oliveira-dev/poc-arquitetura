using ApiDefaults.Extensions;

using PaymentService.Api.Contracts.Responses;
using PaymentService.Api.Middlewares;
using PaymentService.Api.Webhooks;
using PaymentService.Application;
using PaymentService.Infrastructure;

namespace PaymentService.Api.Extensions;

public static class ApiCompositionExtensions
{
    public static IServiceCollection AddPaymentApiComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddApiDefaults<GlobalExceptionHandler>(configuration, "payment.localhost", "localhost")
            .AddApiSwagger()
            .AddApiObservability(configuration);

        services.AddApiJwtAuth(configuration, environment);
        services.AddPaymentApplication();
        services.AddPaymentInfrastructure(configuration, environment);
        services.AddScoped<StripeWebhookValidator>();
        services.AddSingleton<PaymentWebhookTelemetry>();

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.CreateResult;
            });
        services.AddEndpointsApiExplorer();

        return services;
    }
}
