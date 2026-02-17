namespace Auth.Api.Extensions;

public static class WebHostBuilderExtensions
{
    /// <summary>
    /// Hardening básico do Kestrel.
    /// </summary>
    public static IWebHostBuilder ConfigureAuthApiKestrel(this IWebHostBuilder webHost)
    {
        webHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
        });

        return webHost;
    }
}
