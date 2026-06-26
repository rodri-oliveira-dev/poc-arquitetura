using IdentityService.Application.Common.DomainEvents;
using IdentityService.Domain.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.DomainEvents;

internal sealed partial class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            await DispatchSingleAsync(domainEvent, cancellationToken);
        }
    }

    private async Task DispatchSingleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler is null)
                continue;

            try
            {
                await InvokeHandlerAsync(handlerType, handler, domainEvent, cancellationToken);
            }
#pragma warning disable CA1031
            catch (Exception exception)
#pragma warning restore CA1031
            {
                DomainEventHandlerFailed(
                    logger,
                    exception,
                    handler.GetType().FullName,
                    domainEvent.GetType().FullName);
            }
        }
    }

    private static Task InvokeHandlerAsync(
        Type handlerType,
        object handler,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var method = handlerType.GetMethod(nameof(IDomainEventHandler<>.HandleAsync))
            ?? throw new InvalidOperationException($"Handler type '{handlerType.FullName}' does not expose HandleAsync.");

        return (Task)(method.Invoke(handler, [domainEvent, cancellationToken])
            ?? throw new InvalidOperationException($"Handler '{handler.GetType().FullName}' returned null."));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Domain event handler {HandlerType} failed for {DomainEventType}.")]
    private static partial void DomainEventHandlerFailed(
        ILogger logger,
        Exception exception,
        string? handlerType,
        string? domainEventType);
}
