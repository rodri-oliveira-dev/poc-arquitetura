using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using MediatR;

using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Common.Exceptions;
using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;

namespace TransferService.Application.Transferencias.Commands;

public sealed class SolicitarTransferenciaCommandHandler
    : IRequestHandler<SolicitarTransferenciaCommand, SolicitarTransferenciaResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITransferenciaSagaRepository _sagaRepository;
    private readonly ITransferenciaIdempotencyService _idempotencyService;
    private readonly ITransferenciaOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SolicitarTransferenciaCommandHandler(
        ITransferenciaSagaRepository sagaRepository,
        ITransferenciaIdempotencyService idempotencyService,
        ITransferenciaOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _sagaRepository = sagaRepository;
        _idempotencyService = idempotencyService;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<SolicitarTransferenciaResult> Handle(
        SolicitarTransferenciaCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceMerchantId = request.SourceMerchantId.Trim();
        var destinationMerchantId = request.DestinationMerchantId.Trim();
        var idempotencyKey = request.IdempotencyKey.Trim();
        var description = Normalize(request.Description);
        var externalReference = Normalize(request.ExternalReference);
        var requestHash = GenerateRequestHash(
            sourceMerchantId,
            destinationMerchantId,
            request.Amount,
            description,
            externalReference);

        var existing = await _idempotencyService.GetAsync(sourceMerchantId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("Idempotency-Key already used with a different payload.");

            return existing.Response with
            {
                IdempotentReplay = true
            };
        }

        var now = _clock.UtcNow;
        var saga = new TransferenciaSaga(
            new MerchantId(sourceMerchantId),
            new MerchantId(destinationMerchantId),
            new TransferAmount(request.Amount),
            now);
        saga.RegisterRequestMetadata(
            idempotencyKey,
            requestHash,
            request.CorrelationId,
            now,
            description,
            externalReference);

        var response = ToResult(saga, idempotentReplay: false);
        var evento = TransferenciaSagaEventFactory.TransferenciaSolicitada(
            saga,
            request.CorrelationId,
            now);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _sagaRepository.AddAsync(saga, cancellationToken);
        await _outboxWriter.WriteAsync(evento, cancellationToken);
        await _idempotencyService.AddAsync(
            sourceMerchantId,
            idempotencyKey,
            requestHash,
            response,
            now.AddDays(7),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return response;
    }

    private static SolicitarTransferenciaResult ToResult(TransferenciaSaga saga, bool idempotentReplay)
        => new(
            saga.Id,
            saga.Status.ToString(),
            saga.SourceMerchantId.Value,
            saga.DestinationMerchantId.Value,
            saga.Amount.Value,
            saga.CreatedAt,
            idempotentReplay);

    private static string GenerateRequestHash(
        string sourceMerchantId,
        string destinationMerchantId,
        decimal amount,
        string? description,
        string? externalReference)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            SourceMerchantId = sourceMerchantId,
            DestinationMerchantId = destinationMerchantId,
            Amount = amount,
            Description = description,
            ExternalReference = externalReference
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
