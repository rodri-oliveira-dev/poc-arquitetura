using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Idempotency;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Policies;
using LedgerService.Domain.Repositories;

using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class SolicitarEstornoLancamentoHandler
    : IRequestHandler<SolicitarEstornoLancamentoCommand, SolicitarEstornoLancamentoResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IEstornoLancamentoRepository _estornoRepository;
    private readonly IIdempotencyRecordRepository _idempotencyRecordRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly LedgerReversalPolicy _reversalPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly LedgerDomainMetrics? _metrics;

    public SolicitarEstornoLancamentoHandler(
        ILedgerEntryRepository ledgerEntryRepository,
        IEstornoLancamentoRepository estornoRepository,
        IIdempotencyRecordRepository idempotencyRecordRepository,
        IOutboxMessageRepository outboxMessageRepository,
        LedgerReversalPolicy reversalPolicy,
        IUnitOfWork unitOfWork,
        IClock? clock = null,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(ledgerEntryRepository);
        ArgumentNullException.ThrowIfNull(estornoRepository);
        ArgumentNullException.ThrowIfNull(idempotencyRecordRepository);
        ArgumentNullException.ThrowIfNull(outboxMessageRepository);
        ArgumentNullException.ThrowIfNull(reversalPolicy);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _ledgerEntryRepository = ledgerEntryRepository;
        _estornoRepository = estornoRepository;
        _idempotencyRecordRepository = idempotencyRecordRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _reversalPolicy = reversalPolicy;
        _unitOfWork = unitOfWork;
        _clock = clock ?? new SystemClock();
        _metrics = metrics;
    }

    public async Task<SolicitarEstornoLancamentoResult> Handle(
        SolicitarEstornoLancamentoCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestHash = GenerateRequestHash(request);
        var correlationId = Guid.Parse(request.CorrelationId);
        var now = _clock.UtcNow.UtcDateTime;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var lancamentoOriginal = await _ledgerEntryRepository.GetByIdAsync(request.LancamentoId, cancellationToken);
        if (lancamentoOriginal is null)
        {
            _metrics?.RecordReversalRequested("not_found");
            throw new NotFoundException("Lancamento original nao encontrado.");
        }

        if (!IsMerchantAuthorized(request.AuthorizedMerchantIds, lancamentoOriginal.MerchantId))
        {
            _metrics?.RecordReversalRequested("rejected");
            throw new ForbiddenException("Token sem autorizacao para o merchant do lancamento original.");
        }

        var existing = await _idempotencyRecordRepository
            .GetByMerchantAndKeyAsync(lancamentoOriginal.MerchantId, request.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                _metrics?.RecordReversalRequested("rejected");
                throw new ConflictException("Idempotency-Key already used with a different payload.");
            }

            if (!string.IsNullOrWhiteSpace(existing.ResponseBody))
            {
                var replay = JsonSerializer.Deserialize<SolicitarEstornoLancamentoResult>(existing.ResponseBody, JsonOptions);
                if (replay is not null)
                {
                    _metrics?.RecordIdempotencyHit("request_reversal");
                    return replay;
                }
            }

            _metrics?.RecordReversalRequested("failed");
            throw new ConflictException("Unable to replay idempotent response.");
        }

        try
        {
            await _reversalPolicy.EnsureCanRequestReversalAsync(lancamentoOriginal, cancellationToken);
        }
        catch (DomainException ex)
        {
            _metrics?.RecordReversalRequested("rejected");
            throw new ConflictException(ex.Message);
        }

        var estorno = new EstornoLancamento(
            request.LancamentoId,
            lancamentoOriginal.MerchantId,
            request.Motivo,
            correlationId,
            now);

        await _estornoRepository.AddAsync(estorno, cancellationToken);

        var response = ToResponse(estorno);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        var idempotencyRecord = new IdempotencyRecord(
            lancamentoOriginal.MerchantId,
            request.IdempotencyKey,
            requestHash,
            lancamentoOriginal.Id,
            202,
            responseJson,
            now,
            now.AddDays(7));

        await _idempotencyRecordRepository.AddAsync(idempotencyRecord, cancellationToken);

        var outboxPayload = JsonSerializer.Serialize(
            new LancamentoEstornoSolicitadoV1(
                estorno.Id,
                estorno.LancamentoOriginalId,
                estorno.MerchantId,
                estorno.Motivo,
                estorno.Status.ToString(),
                estorno.CreatedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                request.CorrelationId),
            JsonOptions);

        var traceContext = OutboxTraceContext.CaptureCurrent();
        var outboxMessage = new OutboxMessage(
            "LancamentoEstorno",
            estorno.Id,
            LancamentoEstornoSolicitadoV1.EventType,
            outboxPayload,
            now,
            correlationId,
            traceContext.TraceParent,
            traceContext.TraceState,
            traceContext.Baggage);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordReversalRequested("success");

        return response;
    }

    private static SolicitarEstornoLancamentoResult ToResponse(EstornoLancamento estorno)
        => new(
            estorno.Id,
            estorno.LancamentoOriginalId,
            estorno.Status.ToString(),
            $"/api/v1/lancamentos/estornos/{estorno.Id}",
            estorno.MerchantId);

    private static bool IsMerchantAuthorized(IReadOnlyCollection<string> authorizedMerchantIds, string merchantId)
        => authorizedMerchantIds.Any(value => string.Equals(value, merchantId, StringComparison.Ordinal));

    private static string GenerateRequestHash(SolicitarEstornoLancamentoCommand request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            request.LancamentoId,
            Motivo = NormalizeText(request.Motivo)
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeText(string value)
        => value.Trim();
}
