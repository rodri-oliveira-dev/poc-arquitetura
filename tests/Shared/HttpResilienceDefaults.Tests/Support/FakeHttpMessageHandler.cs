using System.Net;
using System.Text;

namespace HttpResilienceDefaults.Tests.Support;

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

    public CancellationToken LastCancellationToken
    {
        get; private set;
    }

    public int PendingResponses
        => _responses.Count;

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
            Content = new StringContent(content, Encoding.UTF8, "text/plain")
        }));
    }

    public void EnqueueException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _responses.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(exception));
    }

    public void EnqueueDelay(TimeSpan delay, HttpStatusCode statusCode, string content)
    {
        _responses.Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(delay, cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/plain")
            };
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RequestCount++;
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var response = _responses.Count == 0
            ? throw new InvalidOperationException("No fake HTTP response was configured.")
            : _responses.Dequeue();

        return await response(request, cancellationToken);
    }
}
