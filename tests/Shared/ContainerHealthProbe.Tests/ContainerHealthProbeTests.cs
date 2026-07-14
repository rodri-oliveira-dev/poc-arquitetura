using System.Globalization;
using System.Net;

namespace ContainerHealthProbe.Tests;

public sealed class ContainerHealthProbeTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("8080")]
    [InlineData("65535")]
    public void Valid_port_should_build_loopback_http_uri(string port)
    {
        bool created = ProbeTarget.TryCreate([port, "/ready"], out Uri uri);

        Assert.True(created);
        Assert.Equal("http", uri.Scheme);
        Assert.Equal("127.0.0.1", uri.Host);
        Assert.Equal(int.Parse(port, CultureInfo.InvariantCulture), uri.Port);
        Assert.Equal("/ready", uri.AbsolutePath);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("abc")]
    public void Invalid_port_should_be_rejected(string port)
    {
        bool created = ProbeTarget.TryCreate([port, "/ready"], out _);

        Assert.False(created);
    }

    [Theory]
    [InlineData("/ready")]
    [InlineData("/health")]
    public void Valid_relative_path_should_be_accepted(string path)
    {
        bool created = ProbeTarget.TryCreate(["8080", path], out Uri uri);

        Assert.True(created);
        Assert.Equal(path, uri.AbsolutePath);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("/../ready")]
    [InlineData("/health\\ready")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://127.0.0.1:22")]
    [InlineData("http://localhost:9000")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/ready#fragment")]
    public void Unsafe_path_or_absolute_destination_should_be_rejected(string path)
    {
        bool created = ProbeTarget.TryCreate(["8080", path], out _);

        Assert.False(created);
    }

    [Fact]
    public void User_supplied_hostname_should_never_be_used_in_final_uri()
    {
        bool created = ProbeTarget.TryCreate(["9000", "/ready"], out Uri uri);

        Assert.True(created);
        Assert.Equal("127.0.0.1", uri.Host);
        Assert.Equal(9000, uri.Port);
    }

    [Fact]
    public void Handler_should_disable_redirects()
    {
        using SocketsHttpHandler handler = ProbeTarget.CreateHandler();

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task Runner_should_return_two_for_invalid_arguments()
    {
        int exitCode = await ProbeRunner.RunAsync(["http://169.254.169.254/latest/meta-data"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Runner_should_return_zero_for_healthy_response()
    {
        using var handler = new StubHandler(HttpStatusCode.OK);

        int exitCode = await ProbeRunner.RunAsync(["8080", "/ready"], () => new HttpClient(handler, disposeHandler: false));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Runner_should_return_one_for_unhealthy_response()
    {
        using var handler = new StubHandler(HttpStatusCode.ServiceUnavailable);

        int exitCode = await ProbeRunner.RunAsync(["8080", "/ready"], () => new HttpClient(handler, disposeHandler: false));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Runner_should_return_one_for_http_error()
    {
        using var handler = new ThrowingHandler(new HttpRequestException());

        int exitCode = await ProbeRunner.RunAsync(["8080", "/ready"], () => new HttpClient(handler, disposeHandler: false));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Runner_should_return_one_for_timeout()
    {
        using var handler = new ThrowingHandler(new TaskCanceledException());

        int exitCode = await ProbeRunner.RunAsync(["8080", "/ready"], () => new HttpClient(handler, disposeHandler: false));

        Assert.Equal(1, exitCode);
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("http://127.0.0.1:8080/ready", request.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(exception);
        }
    }
}
