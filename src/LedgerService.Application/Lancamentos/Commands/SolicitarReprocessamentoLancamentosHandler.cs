using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerService.Application.Common.Exceptions;
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

    public SolicitarReprocessamentoLancamentosHandler(
        IReprocessamentoLancamentosRepository reprocessamentoRepository,
        IIdempotencyRecordRepository idempotencyRecordRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork)
    {
        _reprocessamentoRepository = reprocessamentoRepository;
        _idempotencyRecordRepository = idempotencyRecordRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SolicitarReprocessamentoLancamentosResult> Handle(
        SolicitarReprocessamentoLancamentosCommand request,
        CancellationToken cancellationToken)
    {
        if (!IsMerchantAuthorized(request.AuthorizedMerchantIds, request.MerchantId))
            throw new ForbiddenException("Token sem autorizacao para o merchant informado.");

        var requestHash = GenerateRequestHash(request);
        var correlationId = Guid.Parse(request.CorrelationId);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var existing = await _idempotencyRecordRepository
            .GetByMerchantAndKeyAsync(request.MerchantId, request.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("Idempotency-Key already used with a different payload.");

            if (!string.IsNullOrWhiteSpace(existing.ResponseBody))
            {
                var replay = JsonSerializer.Deserialize<SolicitarReprocessamentoLancamentosResult>(
                    existing.ResponseBody,
                    JsonOptions);
                if (replay is not null)
                    return replay;
            }

            throw new ConflictException("Unable to replay idempotent response.");
        }

        var reprocessamento = new ReprocessamentoLancamentos(
            request.MerchantId,
            request.DataInicial,
            request.DataFinal,
            request.Motivo,
            correlationId);

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
            DateTime.Now.AddDays(7));

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

        var outboxMessage = new OutboxMessage(
            "ReprocessamentoLancamentos",
            reprocessamento.Id,
            ReprocessamentoLancamentosSolicitadoV1.EventType,
            outboxPayload,
            DateTime.Now,
            correlationId);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
