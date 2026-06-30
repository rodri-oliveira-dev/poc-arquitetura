namespace IdentityService.Infrastructure.IdentityProvider;

public sealed class KeycloakAdminOptions
{
    public const string SectionName = "IdentityProvider:Keycloak";

    public string? BaseUrl
    {
        get; set;
    }

    public string? Realm
    {
        get; set;
    }

    public string? TokenEndpoint
    {
        get; set;
    }

    public string? ClientId
    {
        get; set;
    }

    public string? ClientSecret
    {
        get; set;
    }

    public TimeSpan Timeout
    {
        get; set;
    } = TimeSpan.FromSeconds(10);
}
