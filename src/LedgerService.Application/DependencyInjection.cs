using FluentValidation;
using LedgerService.Application.Common.Behaviors;
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

        services.AddScoped<CreateLancamentoService>();

        return services;
    }
}
