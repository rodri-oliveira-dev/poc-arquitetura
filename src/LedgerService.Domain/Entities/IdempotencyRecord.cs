using LedgerService.Domain.Common;

namespace LedgerService.Domain.Entities;

public sealed class IdempotencyRecord : Entity
{
    public string MerchantId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string RequestHash { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyRecord()
    {
        MerchantId = string.Empty;
        IdempotencyKey = string.Empty;
        RequestHash = string.Empty;
    }

    public IdempotencyRecord(
        string merchantId,
        string idempotencyKey,
        string requestHash,
        Guid? ledgerEntryId,
        int responseStatusCode,
        string? responseBody,
        DateTime expiresAt)
    {
        MerchantId = merchantId;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        LedgerEntryId = ledgerEntryId;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        CreatedAt = DateTime.Now;
        ExpiresAt = expiresAt;
    }
}