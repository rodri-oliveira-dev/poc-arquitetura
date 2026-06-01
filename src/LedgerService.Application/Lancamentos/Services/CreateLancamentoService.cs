using System.Globalization;

using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Repositories;

namespace LedgerService.Application.Lancamentos.Services;

public sealed class CreateLancamentoService
{
    private const string DefaultCurrency = "BRL";

    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly CreateLancamentoIdempotencyService _idempotencyService;
    private readonly LedgerEntryCreatedOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly LedgerDomainMetrics? _metrics;

    public CreateLancamentoService(
        ILedgerEntryRepository ledgerEntryRepository,
        CreateLancamentoIdempotencyService idempotencyService,
        LedgerEntryCreatedOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        IClock? clock = null,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(ledgerEntryRepository);
        ArgumentNullException.ThrowIfNull(idempotencyService);
        ArgumentNullException.ThrowIfNull(outboxWriter);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _ledgerEntryRepository = ledgerEntryRepository;
        _idempotencyService = idempotencyService;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _clock = clock ?? new SystemClock();
        _metrics = metrics;
    }

    public async Task<LancamentoDto> ExecuteAsync(CreateLancamentoInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestHash = CreateLancamentoIdempotencyService.GenerateRequestHash(request);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var replay = await _idempotencyService.TryReplayAsync(request, requestHash, cancellationToken);
        if (replay is not null)
            return replay;

        var parsedType = request.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase)
            ? LedgerEntryType.Credit
            : LedgerEntryType.Debit;

        var amount = decimal.Parse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture);
        var occurredAt = _clock.UtcNow.UtcDateTime;
        var correlationId = Guid.Parse(request.CorrelationId);

        LedgerEntry ledgerEntry;
        try
        {
            ledgerEntry = new LedgerEntry(
                request.MerchantId,
                parsedType,
                amount,
                occurredAt,
                request.Description,
                request.ExternalReference,
                correlationId,
                occurredAt);
        }
        catch (DomainException)
        {
            _metrics?.RecordEntryRejected("domain_rule_violation");
            throw;
        }

        await _ledgerEntryRepository.AddAsync(ledgerEntry, cancellationToken);

        var response = ToResponse(ledgerEntry);
        await _idempotencyService.AddAsync(
            request,
            requestHash,
            ledgerEntry.Id,
            response,
            occurredAt,
            cancellationToken);
        await _outboxWriter.WriteAsync(ledgerEntry, response, request.CorrelationId, occurredAt, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordEntryCreated(response.Type, DefaultCurrency, "success");

        return response;
    }

    private static LancamentoDto ToResponse(LedgerEntry ledgerEntry)
        => new(
            Id: $"lan_{ledgerEntry.Id.ToString("N")[..8]}",
            MerchantId: ledgerEntry.MerchantId,
            Type: ledgerEntry.Type == LedgerEntryType.Credit ? "CREDIT" : "DEBIT",
            Amount: ledgerEntry.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            OccurredAt: ledgerEntry.OccurredAt.ToString("o", CultureInfo.InvariantCulture),
            Description: ledgerEntry.Description,
            ExternalReference: ledgerEntry.ExternalReference,
            CreatedAt: ledgerEntry.CreatedAt.ToString("o", CultureInfo.InvariantCulture));

}
