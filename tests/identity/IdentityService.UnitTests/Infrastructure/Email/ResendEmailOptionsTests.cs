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

        var welcomeOptions = provider.GetRequiredService<IOptions<WelcomeEmailOptions>>().Value;
        Assert.Equal("Email/Templates/WelcomeEmail.html", welcomeOptions.TemplatePath);
        Assert.Equal("https://auth.localhost/login", welcomeOptions.AuthenticationUrl);
    }
}
