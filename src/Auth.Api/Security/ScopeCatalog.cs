namespace Auth.Api.Security;

/// <summary>
/// Catálogo fixo de scopes aceitos pelo Auth.Api (POC).
/// </summary>
public static class ScopeCatalog
{
    // Requisito: conjunto fixo de scopes por endpoint dos serviços alvos.
    public static readonly IReadOnlyList<string> ValidScopes = new[]
    {
        "ledger.write",
        "balance.read"
    };

    public static string ValidScopesAsString() => string.Join(' ', ValidScopes);
}
