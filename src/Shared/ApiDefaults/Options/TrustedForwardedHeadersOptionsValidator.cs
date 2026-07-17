using System.Net;

using Microsoft.Extensions.Options;

namespace ApiDefaults.Options;

public sealed class TrustedForwardedHeadersOptionsValidator(
    IHostEnvironment environment) : IValidateOptions<TrustedForwardedHeadersOptions>
{
    public ValidateOptionsResult Validate(string? name, TrustedForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        bool isLocal = IsLocalEnvironment(environment);
        if (options.EnableLocalPermissiveMode && !isLocal)
        {
            failures.Add("ForwardedHeaders:EnableLocalPermissiveMode can only be enabled in Development or Local environments.");
        }

        ValidateNotBlank(options.TrustedProxies, "ForwardedHeaders:TrustedProxies", failures);
        ValidateNotBlank(options.TrustedNetworks, "ForwardedHeaders:TrustedNetworks", failures);
        ValidateNotBlank(options.AllowedHosts, "ForwardedHeaders:AllowedHosts", failures);

        if (!isLocal)
        {
            foreach (string host in options.AllowedHosts)
            {
                if (IsLocalForwardedHost(host))
                {
                    failures.Add($"ForwardedHeaders:AllowedHosts cannot use local host '{host}' outside Development and Local environments.");
                }
            }
        }

        foreach (string proxy in options.TrustedProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add($"ForwardedHeaders:TrustedProxies contains an invalid IP address: '{proxy}'.");
            }
        }

        foreach (string network in options.TrustedNetworks)
        {
            if (!TrustedForwardedHeadersParser.TryParseCidr(network, out _, out _))
            {
                failures.Add($"ForwardedHeaders:TrustedNetworks contains an invalid CIDR: '{network}'.");
            }
        }

        if (!isLocal && options.TrustedProxies.Count == 0 && options.TrustedNetworks.Count == 0)
        {
            failures.Add("ForwardedHeaders must configure at least one trusted proxy or trusted network outside Development and Local environments.");
        }

        if (!isLocal && options.AllowedHosts.Count == 0)
        {
            failures.Add("ForwardedHeaders must configure at least one allowed forwarded host outside Development and Local environments.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    internal static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment() || environment.IsEnvironment("Local");

    private static bool IsLocalForwardedHost(string host)
    {
        string normalized = host.Trim().TrimEnd('.');

        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(normalized, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static void ValidateNotBlank(
        IEnumerable<string> values,
        string optionName,
        List<string> failures)
    {
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{optionName} cannot contain empty values.");
        }
    }
}
