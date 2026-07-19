using System.Diagnostics;

using PetShop.Observability.Propagation;

namespace PetShop.Observability.Messaging;

public interface IMessagePropagationHandler
{
    PropagationContextSnapshot CaptureCurrent(
        string? correlationId = null,
        string? tenantId = null);

    void Inject(
        IDictionary<string, string> headers,
        PropagationContextSnapshot context);

    PropagationContextSnapshot Extract(
        IEnumerable<KeyValuePair<string, string>> headers);

    Activity? StartProducerActivity(
        ActivitySource activitySource,
        string operationName,
        string messagingSystem,
        string destinationName,
        PropagationContextSnapshot? parentContext = null);

    Activity? StartConsumerActivity(
        ActivitySource activitySource,
        string operationName,
        string messagingSystem,
        string sourceName,
        PropagationContextSnapshot receivedContext);
}
