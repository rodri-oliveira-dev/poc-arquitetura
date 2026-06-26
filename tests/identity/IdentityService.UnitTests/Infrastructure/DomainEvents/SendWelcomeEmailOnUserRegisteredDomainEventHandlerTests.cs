using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;
using IdentityService.Infrastructure.DomainEvents;
using IdentityService.Infrastructure.Email;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityService.UnitTests.Infrastructure.DomainEvents;

public sealed class SendWelcomeEmailOnUserRegisteredDomainEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_should_send_welcome_email_Async()
    {
        var sender = new RecordingEmailSender();
        var renderer = new RecordingEmailTemplateRenderer("<p>rendered</p>");
        var handler = CreateHandler(renderer, sender);

        await handler.HandleAsync(CreateDomainEvent(), TestContext.Current.CancellationToken);

        var message = Assert.Single(sender.Messages);
        Assert.Equal("user@example.com", message.ToAddress);
        Assert.Equal("identity.user", message.ToName);
        Assert.Equal("Bem-vindo", message.Subject);
        Assert.Equal("<p>rendered</p>", message.HtmlBody);
        Assert.Equal("Email/Templates/WelcomeEmail.html", renderer.TemplatePath);
        Assert.Equal("identity.user", renderer.Values["UserName"]);
        Assert.Equal("merchant-123", renderer.Values["MerchantId"]);
        Assert.Equal("https://auth.localhost/login", renderer.Values["AuthenticationLink"]);
    }

    [Fact]
    public async Task HandleAsync_should_not_throw_when_email_sender_fails_Async()
    {
        var sender = new RecordingEmailSender
        {
            Exception = new InvalidOperationException("smtp failed")
        };
        var handler = CreateHandler(new RecordingEmailTemplateRenderer("<p>rendered</p>"), sender);

        await handler.HandleAsync(CreateDomainEvent(), TestContext.Current.CancellationToken);

        Assert.Single(sender.Messages);
    }

    private static SendWelcomeEmailOnUserRegisteredDomainEventHandler CreateHandler(
        IEmailTemplateRenderer renderer,
        IEmailSender sender)
        => new(
            renderer,
            sender,
            Options.Create(new SmtpEmailOptions
            {
                TemplatePath = "Email/Templates/WelcomeEmail.html",
                AuthenticationUrl = "https://auth.localhost/login"
            }),
            NullLogger<SendWelcomeEmailOnUserRegisteredDomainEventHandler>.Instance);

    private static UserRegisteredDomainEvent CreateDomainEvent()
        => new(
            UserId.New(),
            new IdentityService.Domain.Users.Email("user@example.com"),
            new Username("identity.user"),
            new MerchantId("merchant-123"),
            "kc-user-1",
            new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc));

    private sealed class RecordingEmailTemplateRenderer(string htmlBody) : IEmailTemplateRenderer
    {
        public string? TemplatePath
        {
            get;
            private set;
        }

        public IReadOnlyDictionary<string, string> Values
        {
            get;
            private set;
        } = new Dictionary<string, string>();

        public Task<string> RenderAsync(
            string templatePath,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken = default)
        {
            TemplatePath = templatePath;
            Values = values;
            return Task.FromResult(htmlBody);
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages
        {
            get;
        } = [];

        public Exception? Exception
        {
            get;
            init;
        }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);

            return Exception is null
                ? Task.CompletedTask
                : Task.FromException(Exception);
        }
    }
}
