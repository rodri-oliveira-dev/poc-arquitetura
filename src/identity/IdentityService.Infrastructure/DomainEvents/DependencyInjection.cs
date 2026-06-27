using IdentityService.Application.Common.DomainEvents;
using IdentityService.Domain.Users;

using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.DomainEvents;

internal static class DependencyInjection
{
    public static IServiceCollection AddIdentityDomainEvents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, LogUserRegisteredDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, SendWelcomeEmailOnUserRegisteredDomainEventHandler>();

        return services;
    }
}
