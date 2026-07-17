using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerService.Application.Abstractions.Messaging;
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

    private readonly SolicitarEstornoLancamentoDependencies _dependencies;
    private readonly LedgerReversalPolicy _reversalPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly LedgerDomainMetrics? _metrics;

    public SolicitarEstornoLancamentoHandler(
        SolicitarEstornoLancamentoDependencies dependencies,
        LedgerReversalPolicy reversalPolicy,
        TimeProvider timeProvider,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(reversalPolicy);

        _dependencies = dependencies;
        _reversalPolicy = reversalPolicy;
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _metrics = metrics;
    }

    public async Task<SolicitarEstornoLancamentoResult> Handle(
        SolicitarEstornoLancamentoCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestHash = GenerateRequestHash(request);
        var correlationId = Guid.Parse(request.CorrelationId);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _dependencies.UnitOfWork.BeginTransactionAsync(cancellationToken);

        var lancamentoOriginal = await _dependencies.LedgerEntryRepository.GetByIdAsync(request.LancamentoId, cancellationToken);
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

        var existing = await _dependencies.IdempotencyRecordRepository
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

        await _dependencies.EstornoRepository.AddAsync(estorno, cancellationToken);

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

        await _dependencies.IdempotencyRecordRepository.AddAsync(idempotencyRecord, cancellationToken);

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

        await _dependencies.OutboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        await _dependencies.UnitOfWork.SaveChangesAsync(cancellationToken);
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

public sealed class SolicitarEstornoLancamentoDependencies(
    ILedgerEntryRepository ledgerEntryRepository,
    IEstornoLancamentoRepository estornoRepository,
    IIdempotencyRecordRepository idempotencyRecordRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork)
{
    public ILedgerEntryRepository LedgerEntryRepository { get; } = ledgerEntryRepository;

    public IEstornoLancamentoRepository EstornoRepository { get; } = estornoRepository;

    public IIdempotencyRecordRepository IdempotencyRecordRepository { get; } = idempotencyRecordRepository;

    public IOutboxMessageRepository OutboxMessageRepository { get; } = outboxMessageRepository;

    public IUnitOfWork UnitOfWork { get; } = unitOfWork;
}
