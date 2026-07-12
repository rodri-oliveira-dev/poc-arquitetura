using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Idempotency;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;

namespace LedgerService.Application.Lancamentos.Services;

public sealed class CreateLancamentoIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyRecordRepository _idempotencyRecordRepository;
    private readonly LedgerDomainMetrics? _metrics;

    public CreateLancamentoIdempotencyService(
        IIdempotencyRecordRepository idempotencyRecordRepository,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(idempotencyRecordRepository);

        _idempotencyRecordRepository = idempotencyRecordRepository;
        _metrics = metrics;
    }

    public static string GenerateRequestHash(CreateLancamentoInput request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var canonical = JsonSerializer.Serialize(new
        {
            request.MerchantId,
            Type = request.Type.ToUpperInvariant(),
            request.Amount,
            Description = NormalizeOptionalText(request.Description),
            ExternalReference = NormalizeOptionalText(request.ExternalReference)
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<LancamentoDto?> TryReplayAsync(
        CreateLancamentoInput request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _idempotencyRecordRepository
            .GetByMerchantAndKeyAsync(request.MerchantId, request.IdempotencyKey, cancellationToken);

        if (existing is null)
            return null;

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            _metrics?.RecordEntryRejected("idempotency_conflict");
            throw new ConflictException("Idempotency-Key already used with a different payload.");
        }

        if (!string.IsNullOrWhiteSpace(existing.ResponseBody))
        {
            var replay = JsonSerializer.Deserialize<LancamentoDto>(existing.ResponseBody, JsonOptions);
            if (replay is not null)
            {
                _metrics?.RecordIdempotencyHit("create_entry");
                return replay;
            }
        }

        _metrics?.RecordEntryRejected("idempotency_unreplayable");
        throw new ConflictException("Unable to replay idempotent response.");
    }

    public Task AddAsync(
        CreateLancamentoInput request,
        string requestHash,
        Guid ledgerEntryId,
        LancamentoDto response,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        var responseJson = JsonSerializer.Serialize(response, JsonOptions);
        var idempotencyRecord = new IdempotencyRecord(
            request.MerchantId,
            request.IdempotencyKey,
            requestHash,
            ledgerEntryId,
            201,
            responseJson,
            occurredAt,
            occurredAt.AddDays(7));

        return _idempotencyRecordRepository.AddAsync(idempotencyRecord, cancellationToken);
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
