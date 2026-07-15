using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

using ApiDefaults.Extensions;

using HttpResilienceDefaults;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Polly.CircuitBreaker;

namespace ApiDefaults.Tests.Authentication;

public sealed class JwksDocumentRetrieverTests
{
    private const string EmptyJwksDocument = "{" + "\"keys\":[]" + "}";

    [Fact]
    public async Task GetDocumentAsync_should_succeed_on_first_attempt_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using ServiceProvider provider = CreateProvider(retryCount: 2);
        var sut = CreateRetriever(provider);

        string document = await sut.GetDocumentAsync(server.Address, CancellationToken.None);

        Assert.Equal(EmptyJwksDocument, document);
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public async Task GetDocumentAsync_should_retry_expected_failure_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using ServiceProvider provider = CreateProvider(retryCount: 2);
        var sut = CreateRetriever(provider);

        string document = await sut.GetDocumentAsync(server.Address, CancellationToken.None);

        Assert.Equal(EmptyJwksDocument, document);
        Assert.Equal(2, server.RequestCount);
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using ServiceProvider provider = CreateProvider(retryCount: 2);
        var sut = CreateRetriever(provider);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetDocumentAsync(server.Address, timeout.Token));

        Assert.Equal(3, server.RequestCount);
    }

    [Fact]
    public async Task GetDocumentAsync_should_open_circuit_after_persistent_failures_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using ServiceProvider provider = CreateProvider(
            retryCount: 1,
            new Dictionary<string, string?>
            {
                ["HttpResilience:Clients:JWKS:CircuitBreakerFailureRatio"] = "0.5",
                ["HttpResilience:Clients:JWKS:CircuitBreakerMinimumThroughput"] = "2",
                ["HttpResilience:Clients:JWKS:CircuitBreakerSamplingDuration"] = "00:00:05",
                ["HttpResilience:Clients:JWKS:CircuitBreakerBreakDuration"] = "00:00:05"
            });
        var sut = CreateRetriever(provider);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetDocumentAsync(server.Address, CancellationToken.None));
        int requestsBeforeOpenCircuit = server.RequestCount;

        await Assert.ThrowsAsync<BrokenCircuitException>(() => sut.GetDocumentAsync(server.Address, CancellationToken.None));

        Assert.Equal(2, requestsBeforeOpenCircuit);
        Assert.Equal(requestsBeforeOpenCircuit, server.RequestCount);
    }

    [Fact]
    public async Task GetDocumentAsync_should_allow_recovery_after_break_duration_and_close_circuit_after_success_Async()
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.InternalServerError, "failure"),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using ServiceProvider provider = CreateProvider(
            retryCount: 1,
            CreateFastCircuitBreakerOverrides());
        var sut = CreateRetriever(provider);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetDocumentAsync(server.Address, CancellationToken.None));
        int requestsBeforeOpenCircuit = server.RequestCount;

        await Assert.ThrowsAsync<BrokenCircuitException>(() => sut.GetDocumentAsync(server.Address, CancellationToken.None));
        int requestsAfterFastFailure = server.RequestCount;

        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        string recoveredDocument = await sut.GetDocumentAsync(server.Address, TestContext.Current.CancellationToken);
        string closedCircuitDocument = await sut.GetDocumentAsync(server.Address, TestContext.Current.CancellationToken);

        Assert.Equal(2, requestsBeforeOpenCircuit);
        Assert.Equal(requestsBeforeOpenCircuit, requestsAfterFastFailure);
        Assert.Equal(EmptyJwksDocument, recoveredDocument);
        Assert.Equal(EmptyJwksDocument, closedCircuitDocument);
        Assert.Equal(4, server.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetDocumentAsync_should_not_retry_or_open_circuit_for_business_errors_Async(HttpStatusCode statusCode)
    {
        using var server = await TestHttpServer.StartAsync([
            new HttpResponse(statusCode, "business failure"),
            new HttpResponse(statusCode, "business failure"),
            new HttpResponse(HttpStatusCode.OK, EmptyJwksDocument)
        ]);
        using ServiceProvider provider = CreateProvider(
            retryCount: 1,
            CreateFastCircuitBreakerOverrides());
        var sut = CreateRetriever(provider);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetDocumentAsync(server.Address, CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetDocumentAsync(server.Address, CancellationToken.None));
        string document = await sut.GetDocumentAsync(server.Address, TestContext.Current.CancellationToken);

        Assert.Equal(EmptyJwksDocument, document);
        Assert.Equal(3, server.RequestCount);
    }

    [Fact]
    public async Task GetDocumentAsync_should_treat_timeout_as_transient_failure_Async()
    {
        using var handler = new TimeoutThenSuccessHttpMessageHandler();
        using ServiceProvider provider = CreateProvider(
            retryCount: 1,
            new Dictionary<string, string?>
            {
                ["HttpResilience:Clients:JWKS:AttemptTimeout"] = "00:00:00.250",
                ["HttpResilience:Clients:JWKS:TotalTimeout"] = "00:00:03"
            },
            handler);
        var sut = CreateRetriever(provider);

        string document = await sut.GetDocumentAsync("http://jwks.test/.well-known/jwks.json", TestContext.Current.CancellationToken);

        Assert.Equal(EmptyJwksDocument, document);
        Assert.Equal(2, handler.RequestCount);
    }

    private static ServiceProvider CreateProvider(
        int retryCount,
        Dictionary<string, string?>? overrides = null,
        HttpMessageHandler? primaryHandler = null)
    {
        Dictionary<string, string?> settings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["HttpResilience:Clients:JWKS:TotalTimeout"] = "00:00:05",
            ["HttpResilience:Clients:JWKS:AttemptTimeout"] = "00:00:01",
            ["HttpResilience:Clients:JWKS:RetryCount"] = retryCount.ToString(CultureInfo.InvariantCulture),
            ["HttpResilience:Clients:JWKS:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:JWKS:CircuitBreakerFailureRatio"] = "1",
            ["HttpResilience:Clients:JWKS:CircuitBreakerMinimumThroughput"] = "100",
            ["HttpResilience:Clients:JWKS:CircuitBreakerSamplingDuration"] = "00:00:30",
            ["HttpResilience:Clients:JWKS:CircuitBreakerBreakDuration"] = "00:00:05"
        };

        if (overrides is not null)
        {
            foreach (KeyValuePair<string, string?> option in overrides)
            {
                settings[option.Key] = option.Value;
            }
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        IHttpClientBuilder httpClientBuilder = services
            .AddHttpClient(JwtAuthenticationServiceCollectionExtensions.JwksHttpClientName);

        if (primaryHandler is not null)
        {
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        }

        httpClientBuilder
            .AddConfiguredHttpResilience(configuration, JwtAuthenticationServiceCollectionExtensions.JwksHttpClientName);

        return services.BuildServiceProvider();
    }

    private static JwtAuthenticationServiceCollectionExtensions.JwksHttpClientDocumentRetriever CreateRetriever(
        IServiceProvider provider)
        => new(
            provider.GetRequiredService<IHttpClientFactory>(),
            JwtAuthenticationServiceCollectionExtensions.JwksHttpClientName);

    private static Dictionary<string, string?> CreateFastCircuitBreakerOverrides()
        => new()
        {
            ["HttpResilience:Clients:JWKS:CircuitBreakerFailureRatio"] = "0.5",
            ["HttpResilience:Clients:JWKS:CircuitBreakerMinimumThroughput"] = "2",
            ["HttpResilience:Clients:JWKS:CircuitBreakerSamplingDuration"] = "00:00:05",
            ["HttpResilience:Clients:JWKS:CircuitBreakerBreakDuration"] = "00:00:00.500"
        };

    private sealed class TimeoutThenSuccessHttpMessageHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref _requestCount);

            if (attempt == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The first JWKS attempt should be cancelled by the attempt timeout.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EmptyJwksDocument)
            };
        }
    }

    private sealed record HttpResponse(
        HttpStatusCode StatusCode,
        string Body,
        TimeSpan? DelayBeforeResponse = null);

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
                TcpClient client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                Interlocked.Increment(ref _requestCount);
                _ = Task.Run(() => WriteResponseAndDisposeAsync(client, _stopping.Token), _stopping.Token);
            }
        }

        private async Task WriteResponseAndDisposeAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                try
                {
                    await WriteResponseAsync(client, cancellationToken);
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                }
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

            if (response.DelayBeforeResponse is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
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
}
