using ApplicationDefaults.Behaviors;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.InboxProcessing;
using PaymentService.Application.Payments.Ledger;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IProviderEventMapper, StripeInboxProviderEventMapper>();
        services.AddSingleton(new PaymentInboxProcessingOptions());
        services.AddSingleton(new PaymentLedgerProcessingOptions());
        services.AddScoped<IPaymentLedgerProcessor, PaymentLedgerProcessor>();

        return services;
    }
}
