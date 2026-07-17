using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Idempotency;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;

using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class SolicitarReprocessamentoLancamentosHandler
    : IRequestHandler<SolicitarReprocessamentoLancamentosCommand, SolicitarReprocessamentoLancamentosResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReprocessamentoLancamentosRepository _reprocessamentoRepository;
    private readonly IIdempotencyRecordRepository _idempotencyRecordRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly LedgerDomainMetrics? _metrics;

    public SolicitarReprocessamentoLancamentosHandler(
        IReprocessamentoLancamentosRepository reprocessamentoRepository,
        IIdempotencyRecordRepository idempotencyRecordRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(reprocessamentoRepository);
        ArgumentNullException.ThrowIfNull(idempotencyRecordRepository);
        ArgumentNullException.ThrowIfNull(outboxMessageRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _reprocessamentoRepository = reprocessamentoRepository;
        _idempotencyRecordRepository = idempotencyRecordRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _metrics = metrics;
    }

    public async Task<SolicitarReprocessamentoLancamentosResult> Handle(
        SolicitarReprocessamentoLancamentosCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsMerchantAuthorized(request.AuthorizedMerchantIds, request.MerchantId))
        {
            _metrics?.RecordReprocessRequestCreated("rejected");
            throw new ForbiddenException("Token sem autorizacao para o merchant informado.");
        }

        var requestHash = GenerateRequestHash(request);
        var correlationId = Guid.Parse(request.CorrelationId);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var existing = await _idempotencyRecordRepository
            .GetByMerchantAndKeyAsync(request.MerchantId, request.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                _metrics?.RecordReprocessRequestCreated("rejected");
                throw new ConflictException("Idempotency-Key already used with a different payload.");
            }

            if (!string.IsNullOrWhiteSpace(existing.ResponseBody))
            {
                var replay = JsonSerializer.Deserialize<SolicitarReprocessamentoLancamentosResult>(
                    existing.ResponseBody,
                    JsonOptions);
                if (replay is not null)
                {
                    _metrics?.RecordIdempotencyHit("request_reprocess");
                    return replay;
                }
            }

            _metrics?.RecordReprocessRequestCreated("failed");
            throw new ConflictException("Unable to replay idempotent response.");
        }

        var reprocessamento = new ReprocessamentoLancamentos(
            request.MerchantId,
            request.DataInicial,
            request.DataFinal,
            request.Motivo,
            correlationId,
            now);

        await _reprocessamentoRepository.AddAsync(reprocessamento, cancellationToken);

        var response = ToResponse(reprocessamento);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        var idempotencyRecord = new IdempotencyRecord(
            reprocessamento.MerchantId,
            request.IdempotencyKey,
            requestHash,
            reprocessamento.Id,
            202,
            responseJson,
            now,
            now.AddDays(7));

        await _idempotencyRecordRepository.AddAsync(idempotencyRecord, cancellationToken);

        var outboxPayload = JsonSerializer.Serialize(
            new ReprocessamentoLancamentosSolicitadoV1(
                reprocessamento.Id,
                reprocessamento.MerchantId,
                reprocessamento.DataInicial,
                reprocessamento.DataFinal,
                reprocessamento.Motivo,
                reprocessamento.Status.ToString(),
                reprocessamento.CreatedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                request.CorrelationId),
            JsonOptions);

        var traceContext = OutboxTraceContext.CaptureCurrent();
        var outboxMessage = new OutboxMessage(
            "ReprocessamentoLancamentos",
            reprocessamento.Id,
            ReprocessamentoLancamentosSolicitadoV1.EventType,
            outboxPayload,
            now,
            correlationId,
            traceContext.TraceParent,
            traceContext.TraceState,
            traceContext.Baggage);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordReprocessRequestCreated("success");

        return response;
    }

    private static SolicitarReprocessamentoLancamentosResult ToResponse(ReprocessamentoLancamentos reprocessamento)
        => new(
            reprocessamento.Id,
            reprocessamento.MerchantId,
            reprocessamento.DataInicial,
            reprocessamento.DataFinal,
            reprocessamento.Status.ToString(),
            $"/api/v1/lancamentos/reprocessamentos/{reprocessamento.Id}");

    private static bool IsMerchantAuthorized(IReadOnlyCollection<string> authorizedMerchantIds, string merchantId)
        => authorizedMerchantIds.Any(value => string.Equals(value, merchantId, StringComparison.Ordinal));

    private static string GenerateRequestHash(SolicitarReprocessamentoLancamentosCommand request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            MerchantId = request.MerchantId.Trim(),
            request.DataInicial,
            request.DataFinal,
            Motivo = NormalizeText(request.Motivo)
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeText(string value)
        => value.Trim();
}
