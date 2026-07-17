namespace ApiDefaults.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public bool Enabled
    {
        get; init;
    }

    public IList<string> AllowedOrigins { get; init; } = [];

    public IList<string> AllowedMethods { get; init; } = [];

    public IList<string> AllowedHeaders { get; init; } = [];

    public IList<string> ExposedHeaders { get; init; } = [];

    public bool AllowCredentials
    {
        get; init;
    }

    public int? PreflightMaxAgeSeconds
    {
        get; init;
    }
}
