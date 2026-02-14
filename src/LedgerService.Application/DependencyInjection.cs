using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using LedgerService.Application.Lancamentos.Services;

namespace LedgerService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<CreateLancamentoService>();

        return services;
    }
}
