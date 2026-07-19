using System.Diagnostics;

using PetShop.Observability.Context;
using PetShop.Observability.Propagation;

namespace PetShop.Observability.Messaging;

public sealed class MessagePropagationHandler : IMessagePropagationHandler
{
    private readonly IExecutionContextAccessor _executionContextAccessor;

    public MessagePropagationHandler(IExecutionContextAccessor executionContextAccessor)
    {
        _executionContextAccessor = executionContextAccessor;
    }

    public PropagationContextSnapshot CaptureCurrent(
        string? correlationId = null,
        string? tenantId = null)
    {
        PropagationContextSnapshot? ambient = _executionContextAccessor.Current;

        return PropagationContextSnapshot.CaptureCurrent(
            correlationId ?? ambient?.CorrelationId,
            tenantId ?? ambient?.TenantId);
    }

    public void Inject(
        IDictionary<string, string> headers,
        PropagationContextSnapshot context)
    {
        PropagationHeaders.Inject(headers, context);
    }

    public PropagationContextSnapshot Extract(
        IEnumerable<KeyValuePair<string, string>> headers)
    {
        return PropagationHeaders.Extract(headers);
    }

    public Activity? StartProducerActivity(
        ActivitySource activitySource,
        string operationName,
        string messagingSystem,
        string destinationName,
        PropagationContextSnapshot? parentContext = null)
    {
        PropagationContextSnapshot? effectiveParent = parentContext;
        if (effectiveParent is null && Activity.Current is null)
        {
            effectiveParent = _executionContextAccessor.Current;
        }

        return MessageActivityFactory.Start(
            activitySource,
            operationName,
            ActivityKind.Producer,
            messagingSystem,
            destinationName,
            "publish",
            effectiveParent);
    }

    public Activity? StartConsumerActivity(
        ActivitySource activitySource,
        string operationName,
        string messagingSystem,
        string sourceName,
        PropagationContextSnapshot receivedContext)
    {
        return MessageActivityFactory.Start(
            activitySource,
            operationName,
            ActivityKind.Consumer,
            messagingSystem,
            sourceName,
            "process",
            receivedContext);
    }
}
