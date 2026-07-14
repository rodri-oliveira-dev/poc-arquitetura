using Microsoft.Extensions.Options;

namespace ApiDefaults.Authentication;

public sealed class ApiJwtAuthenticationOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<ApiJwtAuthenticationOptions>
{
    internal const int MaxClockSkewSeconds = 300;

    public ValidateOptionsResult Validate(string? name, ApiJwtAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name is not null && name.Length > 0)
        {
            return ValidateOptionsResult.Skip;
        }

        List<string> failures = [];

        ValidateRequired(options.SectionName, nameof(options.Issuer), options.Issuer, failures);
        ValidateRequired(options.SectionName, nameof(options.Audience), options.Audience, failures);
        ValidateRequired(options.SectionName, nameof(options.JwksUrl), options.JwksUrl, failures);
        ValidateJwksUrl(options, failures);
        ValidateHttpsMetadata(options, failures);
        ValidateClockSkew(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRequired(
        string sectionName,
        string optionName,
        string value,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{sectionName}:{optionName} e obrigatorio.");
        }
    }

    private void ValidateHttpsMetadata(ApiJwtAuthenticationOptions options, List<string> failures)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Local") || environment.IsEnvironment("Test"))
        {
            return;
        }

        if (!options.RequireHttpsMetadata)
        {
            failures.Add($"{options.SectionName}:RequireHttpsMetadata=false e permitido apenas em Development/Local/Test.");
        }

        if (Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out Uri? jwksUri)
            && !string.Equals(jwksUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{options.SectionName}:JwksUrl deve usar HTTPS fora de Development/Local/Test.");
        }
    }

    private static void ValidateJwksUrl(ApiJwtAuthenticationOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.JwksUrl))
        {
            return;
        }

        if (!Uri.TryCreate(options.JwksUrl.Trim(), UriKind.Absolute, out Uri? jwksUri)
            || (jwksUri.Scheme != Uri.UriSchemeHttps && jwksUri.Scheme != Uri.UriSchemeHttp))
        {
            failures.Add($"{options.SectionName}:JwksUrl deve ser uma URI absoluta HTTP ou HTTPS.");
        }
    }

    private static void ValidateClockSkew(ApiJwtAuthenticationOptions options, List<string> failures)
    {
        if (options.ClockSkewSeconds < 0)
        {
            failures.Add($"{options.SectionName}:ClockSkewSeconds nao pode ser negativo.");
        }
        else if (options.ClockSkewSeconds > MaxClockSkewSeconds)
        {
            failures.Add($"{options.SectionName}:ClockSkewSeconds deve ser menor ou igual a {MaxClockSkewSeconds}.");
        }
    }
}
