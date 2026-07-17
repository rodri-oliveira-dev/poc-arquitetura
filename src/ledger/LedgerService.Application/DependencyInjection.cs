using ApplicationDefaults.Behaviors;

using FluentValidation;

using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Application.Outbox.Retry;
using LedgerService.Domain.Policies;

using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LedgerDomainMetrics>();
        services.AddScoped<CreateLancamentoIdempotencyService>();
        services.AddScoped<LedgerEntryCreatedOutboxWriter>();
        services.AddScoped<ProcessarEstornoLancamentoDependencies>();
        services.AddScoped<SolicitarEstornoLancamentoDependencies>();
        services.AddScoped<LedgerReversalPolicy>();
        services.AddSingleton<IJitterProvider, CryptographicJitterProvider>();
        services.AddSingleton<IRetryStrategy, ExponentialBackoffRetryStrategy>();

        return services;
    }
}
