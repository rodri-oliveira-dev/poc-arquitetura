using System.Net;
using System.Text;

namespace PaymentService.IntegrationTests.Infrastructure.Gateway;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();

    public HttpRequestMessage? LastRequest
    {
        get; private set;
    }

    public string? LastRequestBody
    {
        get; private set;
    }

    public int RequestCount
    {
        get; private set;
    }

    public void EnqueueJson(HttpStatusCode statusCode, string json)
    {
        _responses.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
    }

    public void Enqueue(HttpStatusCode statusCode, string content)
    {
        _responses.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        }));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return _responses.Count == 0
            ? throw new InvalidOperationException("No fake HTTP response was configured.")
            : await _responses.Dequeue()(request, cancellationToken);
    }
}
