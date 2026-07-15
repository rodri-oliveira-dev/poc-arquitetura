using System.Diagnostics.CodeAnalysis;
using System.Net;

using ApiDefaults.Extensions;

namespace ApiDefaults.Tests.Authentication;

public sealed class JwksDocumentRetrieverFakeHandlerTests
{
    private const string EmptyJwksDocument = "{" + "\"keys\":[]" + "}";

    [Fact]
    public async Task GetDocumentAsync_should_send_configured_address_without_real_network_call_Async()
    {
        using var context = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EmptyJwksDocument)
        });

        string result = await context.Retriever.GetDocumentAsync("https://issuer.example/jwks", TestContext.Current.CancellationToken);

        Assert.Equal(EmptyJwksDocument, result);
        Assert.Equal(new Uri("https://issuer.example/jwks"), context.Handler.Requests.Single().RequestUri);
    }

    [Fact]
    public async Task GetDocumentAsync_should_throw_for_non_successful_response_Async()
    {
        using var context = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => context.Retriever.GetDocumentAsync("https://issuer.example/jwks", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetDocumentAsync_should_return_invalid_content_without_swallowing_it_Async()
    {
        using var context = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });

        string result = await context.Retriever.GetDocumentAsync("https://issuer.example/jwks", TestContext.Current.CancellationToken);

        Assert.Equal("not-json", result);
    }

    [Fact]
    public async Task GetDocumentAsync_should_propagate_cancellation_Async()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var context = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Retriever.GetDocumentAsync("https://issuer.example/jwks", cancellation.Token));
    }

    [Fact]
    public async Task GetDocumentAsync_should_propagate_http_request_exception_Async()
    {
        using var context = CreateRetriever(_ => throw new HttpRequestException("network failure"));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => context.Retriever.GetDocumentAsync("https://issuer.example/jwks", TestContext.Current.CancellationToken));

        Assert.Equal("network failure", exception.Message);
    }

    [Fact]
    public async Task GetDocumentAsync_should_not_follow_redirects_when_fake_handler_returns_redirect_Async()
    {
        using var context = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://evil.example/jwks") }
        });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => context.Retriever.GetDocumentAsync("https://issuer.example/jwks", TestContext.Current.CancellationToken));

        Assert.Single(context.Handler.Requests);
        Assert.Equal(new Uri("https://issuer.example/jwks"), context.Handler.Requests.Single().RequestUri);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "RetrieverContext owns and disposes the HttpClient and handler.")]
    private static RetrieverContext CreateRetriever(Func<HttpRequestMessage, HttpResponseMessage> send)
    {
        var handler = new FakeHttpMessageHandler(send);
        var client = new HttpClient(handler);
        var retriever = new JwtAuthenticationServiceCollectionExtensions.JwksHttpClientDocumentRetriever(
                new StaticHttpClientFactory(client),
                JwtAuthenticationServiceCollectionExtensions.JwksHttpClientName);

        return new RetrieverContext(handler, client, retriever);
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(send(request));
        }
    }

    private sealed class RetrieverContext(
        FakeHttpMessageHandler handler,
        HttpClient client,
        JwtAuthenticationServiceCollectionExtensions.JwksHttpClientDocumentRetriever retriever) : IDisposable
    {
        public FakeHttpMessageHandler Handler => handler;
        public JwtAuthenticationServiceCollectionExtensions.JwksHttpClientDocumentRetriever Retriever => retriever;

        public void Dispose() => client.Dispose();
    }
}
