using System.Diagnostics;

using PetShop.Observability.Context;
using PetShop.Observability.Propagation;

namespace PetShop.Observability.Http;

public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IExecutionContextAccessor _executionContextAccessor;

    public CorrelationIdDelegatingHandler(IExecutionContextAccessor executionContextAccessor)
    {
        _executionContextAccessor = executionContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? correlationId = CorrelationIdNormalizer.Normalize(
            _executionContextAccessor.Current?.CorrelationId ??
            Activity.Current?.GetBaggageItem(PropagationHeaderNames.CorrelationId));

        if (correlationId is not null)
        {
            request.Headers.Remove(PropagationHeaderNames.HttpCorrelationId);
            request.Headers.TryAddWithoutValidation(
                PropagationHeaderNames.HttpCorrelationId,
                correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
