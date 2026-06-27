using IdentityService.Infrastructure.Email;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Resend;

using ApplicationEmailMessage = IdentityService.Application.Users.Ports.EmailMessage;
using ResendEmailMessage = Resend.EmailMessage;

namespace IdentityService.UnitTests.Infrastructure.Email;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task SendAsync_should_send_rendered_html_using_resend_sdk_Async()
    {
        ResendEmailMessage? sentMessage = null;
        var resend = new Mock<IResend>();
        resend
            .Setup(client => client.EmailSendAsync(
                It.IsAny<ResendEmailMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<ResendEmailMessage, CancellationToken>((message, _) => sentMessage = message)
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), new ResendRateLimit()));
        var sender = CreateSender(resend.Object);

        await sender.SendAsync(
            new ApplicationEmailMessage(
                "user@example.com",
                "Identity User",
                "Bem-vindo",
                "<p>rendered</p>"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(sentMessage);
        Assert.Equal("POC Arquitetura <onboarding@resend.dev>", sentMessage.From.ToString());
        Assert.Contains("user@example.com", sentMessage.To!.Select(address => address.ToString()));
        Assert.Contains("reply@example.com", sentMessage.ReplyTo!.Select(address => address.ToString()));
        Assert.Equal("Bem-vindo", sentMessage.Subject);
        Assert.Equal("<p>rendered</p>", sentMessage.HtmlBody);
    }

    [Fact]
    public async Task SendAsync_should_log_and_rethrow_when_resend_fails_Async()
    {
        var resend = new Mock<IResend>();
        resend
            .Setup(client => client.EmailSendAsync(
                It.IsAny<ResendEmailMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("resend failed"));
        var logger = new CapturingLogger<ResendEmailSender>();
        var sender = CreateSender(resend.Object, logger);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(
                new ApplicationEmailMessage(
                    "user@example.com",
                    "Identity User",
                    "Bem-vindo",
                    "<p>rendered</p>"),
                TestContext.Current.CancellationToken));

        Assert.Equal("resend failed", exception.Message);
        Assert.Contains(logger.Messages, message => message.Contains("Resend email failed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("user@example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_should_fail_when_required_options_are_missing_Async()
    {
        var sender = new ResendEmailSender(
            new RecordingResendClientFactory(Mock.Of<IResend>()),
            Options.Create(new ResendOptions
            {
                From = "onboarding@resend.dev"
            }),
            new CapturingLogger<ResendEmailSender>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(
                new ApplicationEmailMessage(
                    "user@example.com",
                    "Identity User",
                    "Bem-vindo",
                    "<p>rendered</p>"),
                TestContext.Current.CancellationToken));

        Assert.Contains("Resend:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    private static ResendEmailSender CreateSender(
        IResend resend,
        CapturingLogger<ResendEmailSender>? logger = null)
        => new(
            new RecordingResendClientFactory(resend),
            Options.Create(new ResendOptions
            {
                ApiKey = "re_test_key",
                From = "onboarding@resend.dev",
                FromName = "POC Arquitetura",
                ReplyTo = "reply@example.com"
            }),
            logger ?? new CapturingLogger<ResendEmailSender>());

    private sealed class RecordingResendClientFactory(IResend resend) : IResendClientFactory
    {
        public IResend CreateClient()
            => resend;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages
        {
            get;
        } = [];

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
            Messages.Add(formatter(state, exception));

            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
