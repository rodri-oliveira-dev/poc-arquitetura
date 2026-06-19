using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using ApiDefaults.Authentication;
using ApiDefaults.Extensions;

namespace TransferService.IntegrationTests.Api.Security;

[Collection("JWKS document retriever trace")]
public sealed class JwksDocumentRetrieverTests
{
    private const string EmptyJwksDocument = "{" + "\"keys\":[]" + "}";

    [Fact]
    public async Task GetDocumentAsync_should_succeed_on_first_attempt_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using var trace = TraceCapture.Start();
        var sut = CreateRetriever(retryCount: 2);

        string document = await sut.GetDocumentAsync(server.Address, CancellationToken.None);

        Assert.Equal(EmptyJwksDocument, document);
        Assert.Equal(1, server.RequestCount);
        Assert.Empty(trace.Messages);
    }

    [Fact]
    public async Task GetDocumentAsync_should_retry_expected_failure_and_log_warning_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using var trace = TraceCapture.Start();
        var sut = CreateRetriever(retryCount: 2);

        string document = await sut.GetDocumentAsync(server.Address, CancellationToken.None);

        Assert.Equal(EmptyJwksDocument, document);
        Assert.Equal(2, server.RequestCount);
        string message = Assert.Single(trace.JwksWarnings);
        Assert.Contains("JWKS fetch failed. Retrying attempt 1/3.", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDocumentAsync_should_fail_after_configured_attempts_without_infinite_loop_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using var trace = TraceCapture.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sut = CreateRetriever(retryCount: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetDocumentAsync(server.Address, timeout.Token));

        Assert.Equal(3, server.RequestCount);
        Assert.Equal(2, trace.JwksWarnings.Length);
        Assert.Contains(trace.JwksWarnings, message => message.Contains("Retrying attempt 1/3.", StringComparison.Ordinal));
        Assert.Contains(trace.JwksWarnings, message => message.Contains("Retrying attempt 2/3.", StringComparison.Ordinal));
    }

    private static JwtAuthenticationServiceCollectionExtensions.RetryableJwksDocumentRetriever CreateRetriever(
        int retryCount)
        => new(new ApiJwtAuthenticationOptions(
            "Jwt",
            "https://auth-api",
            "transfer-api",
            "https://auth-api/jwks.json",
            RequireHttpsMetadata: true,
            JwksTimeoutSeconds: 1,
            JwksRetryCount: retryCount,
            JwksRetryBaseDelayMilliseconds: 1));

    private sealed record HttpResponse(HttpStatusCode StatusCode, string Body);

    private sealed class TestHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly ConcurrentQueue<HttpResponse> _responses;
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _serverTask;
        private int _requestCount;

        private TestHttpServer(TcpListener listener, IEnumerable<HttpResponse> responses)
        {
            _listener = listener;
            _responses = new ConcurrentQueue<HttpResponse>(responses);
            _serverTask = Task.Run(AcceptRequestsAsync);
        }

        public string Address
        {
            get
            {
                var endpoint = (IPEndPoint)_listener.LocalEndpoint;
                return $"http://127.0.0.1:{endpoint.Port}/jwks";
            }
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public static Task<TestHttpServer> StartAsync(IEnumerable<HttpResponse> responses)
        {
            var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();
            return Task.FromResult(new TestHttpServer(listener, responses));
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();

            try
            {
                _serverTask.GetAwaiter().GetResult();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }

            _stopping.Dispose();
        }

        private async Task AcceptRequestsAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                Interlocked.Increment(ref _requestCount);
                await WriteResponseAsync(client, _stopping.Token);
            }
        }

        private async Task WriteResponseAsync(TcpClient client, CancellationToken cancellationToken)
        {
            NetworkStream stream = client.GetStream();
            await ReadRequestHeadersAsync(stream, cancellationToken);

            if (!_responses.TryDequeue(out HttpResponse? response))
            {
                response = new HttpResponse(HttpStatusCode.InternalServerError, "No response configured.");
            }

            byte[] body = Encoding.UTF8.GetBytes(response.Body);
            string headers =
                $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n" +
                "Content-Type: application/json\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(headerBytes, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task ReadRequestHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1];
            var header = new StringBuilder();

            while (!header.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                int read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                header.Append((char)buffer[0]);
            }
        }
    }

    private sealed class TraceCapture : TraceListener, IDisposable
    {
        private readonly ConcurrentQueue<string> _messages = [];

        private TraceCapture()
        {
        }

        public string[] Messages => [.. _messages];

        public string[] JwksWarnings => [.. _messages
            .Where(message => message.Contains("JWKS fetch failed.", StringComparison.Ordinal))
        ];

        public static TraceCapture Start()
        {
            var listener = new TraceCapture();
            Trace.Listeners.Add(listener);
            return listener;
        }

        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _messages.Enqueue(message);
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _messages.Enqueue(message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Trace.Listeners.Remove(this);
            }

            base.Dispose(disposing);
        }
    }
}

[CollectionDefinition("JWKS document retriever trace", DisableParallelization = true)]
public sealed class JwksDocumentRetrieverTraceCollection;
