using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Transferencias.Commands;

namespace TransferService.Infrastructure.Persistence.Repositories;

public sealed class TransferenciaIdempotencyService(TransferServiceDbContext context, TimeProvider timeProvider) : ITransferenciaIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TransferServiceDbContext _context = context;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<TransferenciaIdempotencyEntry?> GetAsync(
        string sourceMerchantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var record = await _context.IdempotencyRecords
            .AsNoTracking()
            .Where(x =>
                x.SourceMerchantId == sourceMerchantId &&
                x.IdempotencyKey == idempotencyKey &&
                x.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
            return null;

        var response = JsonSerializer.Deserialize<SolicitarTransferenciaResult>(
            record.ResponseBody,
            JsonOptions) ?? throw new InvalidOperationException("Registro de idempotencia do TransferService possui response_body invalido.");

        return new TransferenciaIdempotencyEntry(record.RequestHash, response);
    }

    public async Task AddAsync(
        string sourceMerchantId,
        string idempotencyKey,
        string requestHash,
        SolicitarTransferenciaResult response,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var responseBody = JsonSerializer.Serialize(response, JsonOptions);
        var record = new TransferenciaIdempotencyRecord(
            sourceMerchantId,
            idempotencyKey,
            requestHash,
            response,
            responseBody,
            response.CreatedAt,
            expiresAt);

        await _context.IdempotencyRecords.AddAsync(record, cancellationToken);
    }
}
