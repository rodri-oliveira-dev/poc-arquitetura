using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace ApiDefaults.Options;

public sealed class TrustedForwardedHeadersPostConfigureOptions(
    IHostEnvironment environment,
    IOptions<TrustedForwardedHeadersOptions> trustedOptions) : IPostConfigureOptions<ForwardedHeadersOptions>
{
    public void PostConfigure(string? name, ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        TrustedForwardedHeadersOptions trusted = trustedOptions.Value;

        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 1;

        options.AllowedHosts.Clear();
        foreach (string host in trusted.AllowedHosts.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            options.AllowedHosts.Add(host);
        }

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        if (TrustedForwardedHeadersOptionsValidator.IsLocalEnvironment(environment) &&
            trusted.EnableLocalPermissiveMode)
        {
            return;
        }

        foreach (string proxy in trusted.TrustedProxies.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TrustedForwardedHeadersParser.TryParseIpAddress(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (string cidr in trusted.TrustedNetworks.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TrustedForwardedHeadersParser.TryParseCidr(cidr, out System.Net.IPNetwork network, out _))
            {
                options.KnownIPNetworks.Add(network);
            }
        }
    }
}
