using ApiDefaults.Extensions;

using Microsoft.Extensions.Options;

namespace ApiDefaults.Options;

internal sealed class CorsPolicyPostConfigureOptions(IOptions<CorsOptions> apiCorsOptions)
    : IPostConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>
{
    public void PostConfigure(string? name, Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        CorsOptions cors = apiCorsOptions.Value;

        options.AddPolicy(ApiDefaultsServiceCollectionExtensions.CorsPolicyName, policy =>
        {
            if (!cors.Enabled || cors.AllowedOrigins.Count == 0)
            {
                return;
            }

            policy.WithOrigins([.. cors.AllowedOrigins.Select(origin => origin.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)]);

            if (cors.AllowedMethods.Count > 0)
            {
                policy.WithMethods([.. cors.AllowedMethods.Select(method => method.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal)]);
            }

            if (cors.AllowedHeaders.Count > 0)
            {
                policy.WithHeaders([.. cors.AllowedHeaders.Select(header => header.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)]);
            }

            if (cors.ExposedHeaders.Count > 0)
            {
                policy.WithExposedHeaders([.. cors.ExposedHeaders.Select(header => header.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)]);
            }

            if (cors.AllowCredentials)
            {
                policy.AllowCredentials();
            }

            if (cors.PreflightMaxAgeSeconds is { } seconds)
            {
                policy.SetPreflightMaxAge(TimeSpan.FromSeconds(seconds));
            }
        });
    }
}
