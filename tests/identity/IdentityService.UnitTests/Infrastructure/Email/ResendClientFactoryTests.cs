using IdentityService.Infrastructure.Email;

using Microsoft.Extensions.Options;

namespace IdentityService.UnitTests.Infrastructure.Email;

public sealed class ResendClientFactoryTests
{
    [Fact]
    public void CreateClient_should_return_official_resend_client_when_api_key_is_configured()
    {
        using var httpClient = new HttpClient();
        var factory = new ResendClientFactory(
            httpClient,
            Options.Create(new ResendOptions
            {
                ApiKey = "re_test_key",
                From = "onboarding@resend.dev"
            }));

        var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_should_fail_when_api_key_is_missing()
    {
        using var httpClient = new HttpClient();
        var factory = new ResendClientFactory(
            httpClient,
            Options.Create(new ResendOptions
            {
                From = "onboarding@resend.dev"
            }));

        var exception = Assert.Throws<InvalidOperationException>(factory.CreateClient);

        Assert.Contains("Resend:ApiKey", exception.Message, StringComparison.Ordinal);
    }
}
