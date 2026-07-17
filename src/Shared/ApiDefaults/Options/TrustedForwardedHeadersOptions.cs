namespace ApiDefaults.Options;

public sealed class TrustedForwardedHeadersOptions
{
    public const string SectionName = "ForwardedHeaders";

    public IList<string> TrustedProxies { get; init; } = [];

    public IList<string> TrustedNetworks { get; init; } = [];

    public IList<string> AllowedHosts { get; init; } = [];

    public bool EnableLocalPermissiveMode
    {
        get; init;
    }
}
