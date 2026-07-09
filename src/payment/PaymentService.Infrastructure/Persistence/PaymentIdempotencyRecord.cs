namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentIdempotencyRecord
{
    private PaymentIdempotencyRecord()
    {
    }

    public PaymentIdempotencyRecord(
        Guid id,
        string merchantId,
        string idempotencyKey,
        string requestHash,
        string responseBody,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        MerchantId = merchantId;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        ResponseBody = responseBody;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id
    {
        get; private set;
    }

    public string MerchantId
    {
        get; private set;
    } = string.Empty;

    public string IdempotencyKey
    {
        get; private set;
    } = string.Empty;

    public string RequestHash
    {
        get; private set;
    } = string.Empty;

    public string ResponseBody
    {
        get; private set;
    } = string.Empty;

    public DateTimeOffset CreatedAt
    {
        get; private set;
    }

    public DateTimeOffset ExpiresAt
    {
        get; private set;
    }

    public void UpdateResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            throw new ArgumentException("Response body de idempotencia nao pode ser vazio.", nameof(responseBody));

        ResponseBody = responseBody;
    }
}
