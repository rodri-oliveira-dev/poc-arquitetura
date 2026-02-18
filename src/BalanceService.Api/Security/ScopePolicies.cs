namespace BalanceService.Api.Security;

/// <summary>
/// Catálogo local de scopes e policies da API.
/// </summary>
public static class ScopePolicies
{
    // Requisito do token: claim "scope" (string) com scopes separados por espaço.
    public const string ClaimType = "scope";

    public const string BalanceRead = "balance.read";
    public const string BalanceWrite = "balance.write";

    public const string BalanceReadPolicy = "scope:balance.read";
    public const string BalanceWritePolicy = "scope:balance.write";
}