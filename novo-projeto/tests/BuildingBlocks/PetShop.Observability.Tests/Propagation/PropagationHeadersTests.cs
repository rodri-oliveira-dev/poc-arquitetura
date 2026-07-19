using PetShop.Observability.Propagation;

namespace PetShop.Observability.Tests.Propagation;

public sealed class PropagationHeadersTests
{
    [Fact]
    public void InjectAndExtract_ShouldRoundTripSupportedHeaders()
    {
        string correlationId = Guid.NewGuid().ToString();
        const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var expected = new PropagationContextSnapshot(
            correlationId,
            "tenant-a",
            traceParent,
            "vendor=value",
            "region=south%20america");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        PropagationHeaders.Inject(headers, expected);
        PropagationContextSnapshot actual = PropagationHeaders.Extract(headers);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Extract_ShouldReadHeadersCaseInsensitivelyAndRejectInvalidCorrelationId()
    {
        var headers = new Dictionary<string, string>
        {
            ["TRACEPARENT"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            ["TENANT_ID"] = "tenant-b",
            ["CORRELATION_ID"] = "not-a-guid"
        };

        PropagationContextSnapshot actual = PropagationHeaders.Extract(headers);

        Assert.Null(actual.CorrelationId);
        Assert.Equal("tenant-b", actual.TenantId);
        Assert.NotNull(actual.TraceParent);
    }

    [Fact]
    public void Inject_ShouldRemoveKnownHeaderWhenContextValueIsEmpty()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PropagationHeaderNames.TraceState] = "old-value"
        };

        PropagationHeaders.Inject(headers, default);

        Assert.DoesNotContain(PropagationHeaderNames.TraceState, headers.Keys);
    }
}
