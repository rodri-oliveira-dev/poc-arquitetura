using System.Net;

using PetShop.Observability.Context;
using PetShop.Observability.Http;
using PetShop.Observability.Propagation;

using Xunit;

namespace PetShop.Observability.Tests.Http;

public sealed class CorrelationIdDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_ShouldPropagateTrustedCorrelationIdWithoutTenantHeader()
    {
        string correlationId = Guid.NewGuid().ToString();
        var accessor = new ExecutionContextAccessor();
        var recordingHandler = new RecordingHandler();
        var propagationHandler = new CorrelationIdDelegatingHandler(accessor)
        {
            InnerHandler = recordingHandler
        };
        using var client = new HttpClient(propagationHandler);
        using IDisposable scope = accessor.Push(new PropagationContextSnapshot(
            correlationId,
            "tenant-a",
            null,
            null,
            null));

        using HttpResponseMessage response = await client.GetAsync(
            "https://example.test/health",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(recordingHandler.Request);
        Assert.True(recordingHandler.Request.Headers.TryGetValues(
            PropagationHeaderNames.HttpCorrelationId,
            out IEnumerable<string>? values));
        Assert.Equal(correlationId, Assert.Single(values!));
        Assert.False(recordingHandler.Request.Headers.Contains(PropagationHeaderNames.TenantId));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
