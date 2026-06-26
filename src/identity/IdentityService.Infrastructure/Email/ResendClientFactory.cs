using Microsoft.Extensions.Options;

using Resend;

namespace IdentityService.Infrastructure.Email;

public sealed class ResendClientFactory(
    HttpClient httpClient,
    IOptions<ResendOptions> options) : IResendClientFactory
{
    public IResend CreateClient()
    {
        var currentOptions = options.Value;
        var apiKey = string.IsNullOrWhiteSpace(currentOptions.ApiKey)
            ? throw new InvalidOperationException("Resend:ApiKey nao foi configurado.")
            : currentOptions.ApiKey;

        return ResendClient.Create(
            new ResendClientOptions
            {
                ApiToken = apiKey,
                ThrowExceptions = true
            },
            httpClient);
    }
}
