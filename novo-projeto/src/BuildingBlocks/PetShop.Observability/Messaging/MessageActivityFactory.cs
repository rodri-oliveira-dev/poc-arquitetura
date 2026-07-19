using System.Diagnostics;

using PetShop.Observability.Propagation;

namespace PetShop.Observability.Messaging;

internal static class MessageActivityFactory
{
    public static Activity? Start(
        ActivitySource activitySource,
        string operationName,
        ActivityKind kind,
        string messagingSystem,
        string destinationName,
        string operationType,
        PropagationContextSnapshot? parentContext)
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(messagingSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        Activity? activity = StartWithParent(
            activitySource,
            operationName,
            kind,
            parentContext);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag("messaging.system", messagingSystem);
        activity.SetTag("messaging.destination.name", destinationName);
        activity.SetTag("messaging.operation.type", operationType);

        if (parentContext is PropagationContextSnapshot context)
        {
            BaggageCodec.Apply(activity, context.Baggage);

            if (!string.IsNullOrWhiteSpace(context.CorrelationId))
            {
                activity.SetTag(PropagationHeaderNames.CorrelationId, context.CorrelationId);
                activity.SetBaggage(PropagationHeaderNames.CorrelationId, context.CorrelationId);
            }

            if (!string.IsNullOrWhiteSpace(context.TenantId))
            {
                activity.SetTag(PropagationHeaderNames.TenantId, context.TenantId);
                activity.SetBaggage(PropagationHeaderNames.TenantId, context.TenantId);
            }
        }

        return activity;
    }

    private static Activity? StartWithParent(
        ActivitySource activitySource,
        string operationName,
        ActivityKind kind,
        PropagationContextSnapshot? parentContext)
    {
        if (parentContext is PropagationContextSnapshot context &&
            !string.IsNullOrWhiteSpace(context.TraceParent) &&
            ActivityContext.TryParse(context.TraceParent, context.TraceState, out ActivityContext parsedParent))
        {
            return activitySource.StartActivity(operationName, kind, parsedParent);
        }

        return activitySource.StartActivity(operationName, kind);
    }
}
