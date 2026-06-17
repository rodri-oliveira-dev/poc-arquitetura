using System.Globalization;

using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Repositories;
using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class CreateLancamentoCommandHandler
    : IRequestHandler<CreateLancamentoCommand, LancamentoDto>
{
    private const string DefaultCurrency = "BRL";

    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly CreateLancamentoIdempotencyService _idempotencyService;
    private readonly LedgerEntryCreatedOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly LedgerDomainMetrics? _metrics;

    public CreateLancamentoCommandHandler(
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

    public async Task<LancamentoDto> Handle(CreateLancamentoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = request.Input;
        ArgumentNullException.ThrowIfNull(input);

        var requestHash = CreateLancamentoIdempotencyService.GenerateRequestHash(input);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var replay = await _idempotencyService.TryReplayAsync(input, requestHash, cancellationToken);
        if (replay is not null)
            return replay;

        var parsedType = input.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase)
            ? LedgerEntryType.Credit
            : LedgerEntryType.Debit;

        var amount = decimal.Parse(input.Amount, NumberStyles.Number, CultureInfo.InvariantCulture);
        var occurredAt = _clock.UtcNow.UtcDateTime;
        var correlationId = Guid.Parse(input.CorrelationId);

        LedgerEntry ledgerEntry;
        try
        {
            ledgerEntry = new LedgerEntry(
                input.MerchantId,
                parsedType,
                amount,
                occurredAt,
                input.Description,
                input.ExternalReference,
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
            input,
            requestHash,
            ledgerEntry.Id,
            response,
            occurredAt,
            cancellationToken);
        await _outboxWriter.WriteAsync(ledgerEntry, response, input.CorrelationId, occurredAt, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordEntryCreated(response.Type, DefaultCurrency, "success");

        return response;
    }

    private static LancamentoDto ToResponse(LedgerEntry ledgerEntry)
        => new(
            Id: $"lan_{ledgerEntry.Id.ToString("N")[..8]}",
            LancamentoId: ledgerEntry.Id,
            MerchantId: ledgerEntry.MerchantId,
            Type: ledgerEntry.Type == LedgerEntryType.Credit ? "CREDIT" : "DEBIT",
            Amount: ledgerEntry.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            OccurredAt: ledgerEntry.OccurredAt.ToString("o", CultureInfo.InvariantCulture),
            Description: ledgerEntry.Description,
            ExternalReference: ledgerEntry.ExternalReference,
            CreatedAt: ledgerEntry.CreatedAt.ToString("o", CultureInfo.InvariantCulture));

}
