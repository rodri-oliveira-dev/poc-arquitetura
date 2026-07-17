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
        ValidateAllowedHosts(options.AllowedHosts, isLocal, failures);
        ValidateTrustedProxies(options.TrustedProxies, failures);
        ValidateTrustedNetworks(options.TrustedNetworks, failures);
        ValidateRequiredProductionTrust(options, isLocal, failures);

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

    private static void ValidateAllowedHosts(
        IEnumerable<string> allowedHosts,
        bool isLocal,
        List<string> failures)
    {
        if (isLocal)
            return;

        foreach (string host in allowedHosts.Where(IsLocalForwardedHost))
        {
            failures.Add($"ForwardedHeaders:AllowedHosts cannot use local host '{host}' outside Development and Local environments.");
        }
    }

    private static void ValidateTrustedProxies(
        IEnumerable<string> trustedProxies,
        List<string> failures)
    {
        foreach (string proxy in trustedProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add($"ForwardedHeaders:TrustedProxies contains an invalid IP address: '{proxy}'.");
            }
        }
    }

    private static void ValidateTrustedNetworks(
        IEnumerable<string> trustedNetworks,
        List<string> failures)
    {
        foreach (string network in trustedNetworks)
        {
            if (!TrustedForwardedHeadersParser.TryParseCidr(network, out _, out _))
            {
                failures.Add($"ForwardedHeaders:TrustedNetworks contains an invalid CIDR: '{network}'.");
            }
        }
    }

    private static void ValidateRequiredProductionTrust(
        TrustedForwardedHeadersOptions options,
        bool isLocal,
        List<string> failures)
    {
        if (isLocal)
            return;

        if (options.TrustedProxies.Count == 0 && options.TrustedNetworks.Count == 0)
        {
            failures.Add("ForwardedHeaders must configure at least one trusted proxy or trusted network outside Development and Local environments.");
        }

        if (options.AllowedHosts.Count == 0)
        {
            failures.Add("ForwardedHeaders must configure at least one allowed forwarded host outside Development and Local environments.");
        }
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
