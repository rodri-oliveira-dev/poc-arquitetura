using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PetShop.Observability.Context;
using PetShop.Observability.Http;
using PetShop.Observability.Messaging;

namespace PetShop.Observability.DependencyInjection;

public static class ObservabilityPropagationServiceCollectionExtensions
{
    public static IServiceCollection AddPetShopObservabilityPropagation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExecutionContextAccessor, ExecutionContextAccessor>();
        services.TryAddSingleton<IMessagePropagationHandler, MessagePropagationHandler>();
        services.TryAddTransient<CorrelationIdDelegatingHandler>();

        return services;
    }

    public static IHttpClientBuilder AddCorrelationIdPropagation(
        this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
    }
}
