using IdentityService.Application.Users.Ports;
using IdentityService.Infrastructure;
using IdentityService.Infrastructure.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityService.UnitTests.Infrastructure.Email;

public sealed class ResendEmailOptionsTests
{
    [Fact]
    public void AddIdentityEmail_should_bind_resend_options_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = "re_test_key",
                ["Resend:From"] = "onboarding@resend.dev",
                ["Resend:FromName"] = "POC Arquitetura",
                ["Resend:ReplyTo"] = "reply@example.com",
                ["Mailpit:Host"] = "localhost",
                ["Mailpit:Port"] = "1025",
                ["Mailpit:EnableSsl"] = "false",
                ["Mailpit:From"] = "noreply@poc-arquitetura.local",
                ["Mailpit:FromName"] = "POC Arquitetura",
                ["Email:Provider"] = "Resend",
                ["Email:TemplatePath"] = "Email/Templates/WelcomeEmail.html",
                ["Email:AuthenticationUrl"] = "https://auth.localhost/login"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddIdentityEmail(configuration);

        using var provider = services.BuildServiceProvider();

        var resendOptions = provider.GetRequiredService<IOptions<ResendOptions>>().Value;
        Assert.Equal("re_test_key", resendOptions.ApiKey);
        Assert.Equal("onboarding@resend.dev", resendOptions.From);
        Assert.Equal("POC Arquitetura", resendOptions.FromName);
        Assert.Equal("reply@example.com", resendOptions.ReplyTo);

        var providerOptions = provider.GetRequiredService<IOptions<EmailProviderOptions>>().Value;
        Assert.Equal("Resend", providerOptions.Provider);

        var mailpitOptions = provider.GetRequiredService<IOptions<MailpitOptions>>().Value;
        Assert.Equal("localhost", mailpitOptions.Host);
        Assert.Equal(1025, mailpitOptions.Port);
        Assert.False(mailpitOptions.EnableSsl);
        Assert.Equal("noreply@poc-arquitetura.local", mailpitOptions.From);
        Assert.Equal("POC Arquitetura", mailpitOptions.FromName);

        var welcomeOptions = provider.GetRequiredService<IOptions<WelcomeEmailOptions>>().Value;
        Assert.Equal("Email/Templates/WelcomeEmail.html", welcomeOptions.TemplatePath);
        Assert.Equal("https://auth.localhost/login", welcomeOptions.AuthenticationUrl);
    }

    [Theory]
    [InlineData("Resend", typeof(ResendEmailSender))]
    [InlineData("Mailpit", typeof(MailpitEmailSender))]
    [InlineData("mailpit", typeof(MailpitEmailSender))]
    public void AddIdentityEmail_should_select_email_sender_from_configured_provider(
        string configuredProvider,
        Type expectedSenderType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = configuredProvider,
                ["Resend:ApiKey"] = "re_test_key",
                ["Resend:From"] = "onboarding@resend.dev",
                ["Mailpit:Host"] = "localhost",
                ["Mailpit:Port"] = "1025",
                ["Mailpit:From"] = "noreply@poc-arquitetura.local"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddIdentityEmail(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.IsType(expectedSenderType, provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void AddIdentityEmail_should_reject_unsupported_email_provider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Unknown"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddIdentityEmail(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            provider.GetRequiredService<IEmailSender>);
        Assert.Contains("Email:Provider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Resend, Mailpit", exception.Message, StringComparison.Ordinal);
    }
}
