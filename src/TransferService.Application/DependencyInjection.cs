using Microsoft.Extensions.DependencyInjection;

namespace TransferService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTransferApplication(this IServiceCollection services)
    {
        return services;
    }
}
