namespace LedgerService.Api.Security;

/// <summary>
/// Catálogo local de scopes e policies da API.
/// </summary>
public static class ScopePolicies
{
    // Requisito do token: claim "scope" (string) com scopes separados por espaço.
    public const string ClaimType = "scope";

    public const string LedgerRead = "ledger.read";
    public const string LedgerWrite = "ledger.write";
    public const string OutboxRequeue = "ledger.outbox.requeue";

    public const string LedgerReadPolicy = "scope:ledger.read";
    public const string LedgerWritePolicy = "scope:ledger.write";
    public const string OutboxRequeuePolicy = "scope:ledger.outbox.requeue";
}
