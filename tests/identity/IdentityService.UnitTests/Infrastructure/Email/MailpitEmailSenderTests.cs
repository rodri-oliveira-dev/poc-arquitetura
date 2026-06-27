using IdentityService.Application.Users.Ports;
using IdentityService.Infrastructure.Email;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.UnitTests.Infrastructure.Email;

public sealed class MailpitEmailSenderTests
{
    [Theory]
    [InlineData(null, "localhost", 1025, "Mailpit:From")]
    [InlineData(" ", "localhost", 1025, "Mailpit:From")]
    [InlineData("onboarding@example.com", null, 1025, "Mailpit:Host")]
    [InlineData("onboarding@example.com", " ", 1025, "Mailpit:Host")]
    [InlineData("onboarding@example.com", "localhost", 0, "Mailpit:Port")]
    public async Task SendAsync_should_validate_mailpit_options_before_network_call_Async(
        string? from,
        string? host,
        int port,
        string expectedMessage)
    {
        var sender = new MailpitEmailSender(
            Options.Create(CreateOptions(from, host, port)),
            new CapturingLogger<MailpitEmailSender>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(
                new EmailMessage(
                    "user@example.com",
                    "Identity User",
                    "Bem-vindo",
                    "<p>html</p>"),
                TestContext.Current.CancellationToken));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_should_reject_null_message_Async()
    {
        var sender = new MailpitEmailSender(
            Options.Create(new MailpitOptions
            {
                From = "onboarding@example.com",
                Host = "localhost",
                Port = 1025
            }),
            new CapturingLogger<MailpitEmailSender>());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sender.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    private static MailpitOptions CreateOptions(string? from, string? host, int port)
    {
#pragma warning disable CS8601 // Invalid null values are deliberate for options validation tests.
        return new MailpitOptions
        {
            From = from,
            Host = host,
            Port = port
        };
#pragma warning restore CS8601
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
