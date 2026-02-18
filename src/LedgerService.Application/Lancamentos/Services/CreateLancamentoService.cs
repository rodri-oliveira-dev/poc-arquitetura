using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;

namespace LedgerService.Application.Lancamentos.Services;

public sealed class CreateLancamentoService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IIdempotencyRecordRepository _idempotencyRecordRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLancamentoService(
        ILedgerEntryRepository ledgerEntryRepository,
        IIdempotencyRecordRepository idempotencyRecordRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork)
    {
        _ledgerEntryRepository = ledgerEntryRepository;
        _idempotencyRecordRepository = idempotencyRecordRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<LancamentoDto> ExecuteAsync(CreateLancamentoInput request, CancellationToken cancellationToken)
    {
        var requestHash = GenerateRequestHash(request);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var existing = await _idempotencyRecordRepository
            .GetByMerchantAndKeyAsync(request.MerchantId, request.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("Idempotency-Key already used with a different payload.");

            if (!string.IsNullOrWhiteSpace(existing.ResponseBody))
            {
                var replay = JsonSerializer.Deserialize<LancamentoDto>(existing.ResponseBody, JsonOptions);
                if (replay is not null)
                    return replay;
            }

            throw new ConflictException("Unable to replay idempotent response.");
        }

        var parsedType = request.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase)
            ? LedgerEntryType.Credit
            : LedgerEntryType.Debit;

        var amount = decimal.Parse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture);
        var occurredAt = DateTime.Now;
        var correlationId = Guid.Parse(request.CorrelationId);

        var ledgerEntry = new LedgerEntry(
            request.MerchantId,
            parsedType,
            amount,
            occurredAt,
            request.Description,
            request.ExternalReference,
            correlationId);

        await _ledgerEntryRepository.AddAsync(ledgerEntry, cancellationToken);

        var response = ToResponse(ledgerEntry);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        var idempotencyRecord = new IdempotencyRecord(
            request.MerchantId,
            request.IdempotencyKey,
            requestHash,
            ledgerEntry.Id,
            201,
            responseJson,
            DateTime.Now.AddDays(7));

        await _idempotencyRecordRepository.AddAsync(idempotencyRecord, cancellationToken);

        var outboxPayload = JsonSerializer.Serialize(new
        {
            response.Id,
            response.MerchantId,
            response.Type,
            response.Amount,
            response.OccurredAt,
            response.Description,
            response.ExternalReference,
            response.CreatedAt,
            request.CorrelationId
        }, JsonOptions);

        var outboxMessage = new OutboxMessage(
            "LedgerEntry",
            ledgerEntry.Id,
            "LedgerEntryCreated",
            outboxPayload,
            DateTime.Now,
            correlationId);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return response;
    }

    private static LancamentoDto ToResponse(LedgerEntry ledgerEntry)
        => new(
            Id: $"lan_{ledgerEntry.Id.ToString("N")[..8]}",
            MerchantId: ledgerEntry.MerchantId,
            Type: ledgerEntry.Type == LedgerEntryType.Credit ? "CREDIT" : "DEBIT",
            Amount: ledgerEntry.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            OccurredAt: ledgerEntry.OccurredAt.ToString("o"),
            Description: ledgerEntry.Description,
            ExternalReference: ledgerEntry.ExternalReference,
            CreatedAt: ledgerEntry.CreatedAt.ToString("o"));

    private static string GenerateRequestHash(CreateLancamentoInput request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            request.MerchantId,
            Type = request.Type.ToUpperInvariant(),
            request.Amount
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
