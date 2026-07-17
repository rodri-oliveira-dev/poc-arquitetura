using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

namespace ApiDefaults.Options;

internal sealed class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    private static readonly HashSet<string> AllowedMethods =
    [
        "GET",
        "HEAD",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "OPTIONS"
    ];

    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.PreflightMaxAgeSeconds is < 0)
        {
            failures.Add("Cors:PreflightMaxAgeSeconds must be zero or greater when configured.");
        }

        ValidateOrigins(options, failures);
        ValidateMethods(options.AllowedMethods, failures);
        ValidateHeaders(options.AllowedHeaders, $"{CorsOptions.SectionName}:AllowedHeaders", failures);
        ValidateHeaders(options.ExposedHeaders, $"{CorsOptions.SectionName}:ExposedHeaders", failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateOrigins(CorsOptions options, List<string> failures)
    {
        foreach (string origin in options.AllowedOrigins)
        {
            ValidateOrigin(origin, failures);
        }

        if (options.AllowCredentials &&
            options.AllowedOrigins.Any(origin => origin.Trim() == "*"))
        {
            failures.Add("Cors:AllowCredentials cannot be combined with wildcard origins.");
        }
    }

    private static void ValidateOrigin(string origin, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            failures.Add("Cors:AllowedOrigins must not contain empty values.");
            return;
        }

        if (origin.Contains('*', StringComparison.Ordinal))
        {
            failures.Add($"Cors:AllowedOrigins contains insecure wildcard origin '{origin}'. Configure explicit origins.");
            return;
        }

        if (!TryCreateAbsoluteOrigin(origin, out Uri? uri))
        {
            failures.Add($"Cors:AllowedOrigins contains invalid absolute origin '{origin}'.");
            return;
        }

        ValidateOriginScheme(origin, uri, failures);
        ValidateOriginComponents(origin, uri, failures);
    }

    private static bool TryCreateAbsoluteOrigin(string origin, [NotNullWhen(true)] out Uri? uri)
        => Uri.TryCreate(origin, UriKind.Absolute, out uri) &&
            uri.IsAbsoluteUri &&
            !string.IsNullOrWhiteSpace(uri.Scheme) &&
            !string.IsNullOrWhiteSpace(uri.Host);

    private static void ValidateOriginScheme(string origin, Uri uri, List<string> failures)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Cors:AllowedOrigins origin '{origin}' must use http or https.");
        }
    }

    private static void ValidateOriginComponents(string origin, Uri uri, List<string> failures)
    {
        if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
        {
            failures.Add($"Cors:AllowedOrigins origin '{origin}' must not include a path.");
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            failures.Add($"Cors:AllowedOrigins origin '{origin}' must not include a query string.");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            failures.Add($"Cors:AllowedOrigins origin '{origin}' must not include a fragment.");
        }
    }

    private static void ValidateMethods(IEnumerable<string> methods, List<string> failures)
    {
        foreach (string method in methods)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                failures.Add("Cors:AllowedMethods must not contain empty values.");
                continue;
            }

            string normalized = method.Trim().ToUpperInvariant();
            if (normalized == "*" || !AllowedMethods.Contains(normalized))
            {
                failures.Add($"Cors:AllowedMethods contains unsupported method '{method}'. Configure explicit HTTP methods.");
            }
        }
    }

    private static void ValidateHeaders(IEnumerable<string> headers, string sectionName, List<string> failures)
    {
        foreach (string header in headers.Where(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{sectionName} must not contain empty values.");
        }

        foreach (string header in headers.Where(IsInvalidHeader))
        {
            failures.Add($"{sectionName} contains invalid header '{header}'. Configure explicit HTTP header names.");
        }
    }

    private static bool IsInvalidHeader(string header)
        => !string.IsNullOrWhiteSpace(header) &&
            (header.Trim() == "*" || !IsToken(header));

    private static bool IsToken(string value)
        => value.All(IsTokenCharacter);

    private static bool IsTokenCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) ||
            character is '!' or '#' or '$' or '%' or '&' or '\'' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
}
