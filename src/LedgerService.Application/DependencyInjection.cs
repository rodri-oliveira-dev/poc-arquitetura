using FluentValidation;
using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Behaviors;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Outbox.Retry;
using Microsoft.Extensions.DependencyInjection;
using LedgerService.Application.Lancamentos.Services;
using MediatR;

namespace LedgerService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<LedgerDomainMetrics>();
        services.AddScoped<CreateLancamentoService>();
        services.AddSingleton<IJitterProvider, CryptographicJitterProvider>();
        services.AddSingleton<IRetryStrategy, ExponentialBackoffRetryStrategy>();

        return services;
    }
}
