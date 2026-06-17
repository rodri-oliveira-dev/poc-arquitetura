using TransferService.Application.Transferencias.Commands;

namespace TransferService.Infrastructure.Persistence;

public sealed class TransferenciaIdempotencyRecord
{
    public Guid Id
    {
        get; private set;
    }
    public string SourceMerchantId
    {
        get; private set;
    }
    public string IdempotencyKey
    {
        get; private set;
    }
    public string RequestHash
    {
        get; private set;
    }
    public Guid TransferenciaId
    {
        get; private set;
    }
    public string ResponseBody
    {
        get; private set;
    }
    public DateTimeOffset CreatedAt
    {
        get; private set;
    }
    public DateTimeOffset ExpiresAt
    {
        get; private set;
    }

    private TransferenciaIdempotencyRecord()
    {
        SourceMerchantId = string.Empty;
        IdempotencyKey = string.Empty;
        RequestHash = string.Empty;
        ResponseBody = string.Empty;
    }

    public TransferenciaIdempotencyRecord(
        string sourceMerchantId,
        string idempotencyKey,
        string requestHash,
        SolicitarTransferenciaResult response,
        string responseBody,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceMerchantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseBody);

        Id = Guid.NewGuid();
        SourceMerchantId = sourceMerchantId.Trim();
        IdempotencyKey = idempotencyKey.Trim();
        RequestHash = requestHash.Trim();
        TransferenciaId = response.TransferenciaId;
        ResponseBody = responseBody;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }
}
