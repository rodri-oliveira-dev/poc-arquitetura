using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.Common.Exceptions;
using AuditService.Domain.FunctionalAuditing;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.CreateAuditRecord;

public sealed class CreateAuditRecordCommandHandler(IFunctionalAuditRecordRepository repository)
        : IRequestHandler<CreateAuditRecordCommand, CreateAuditRecordResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFunctionalAuditRecordRepository _repository = repository;

    public async Task<CreateAuditRecordResult> Handle(
        CreateAuditRecordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var idempotencyKey = request.IdempotencyKey.Trim();
        var requestHash = GenerateRequestHash(request);

        CreateAuditRecordResult? replay = await ResolveExistingAsync(idempotencyKey, requestHash, cancellationToken);
        if (replay is not null)
            return replay;

        var record = FunctionalAuditRecord.Create(
            operationId: request.OperationId.ToString(),
            sourceService: request.SourceService,
            operationType: request.OperationType,
            status: request.Status,
            occurredAt: request.OccurredAt,
            correlationId: request.CorrelationId?.ToString(),
            idempotencyKey: idempotencyKey,
            entityType: request.EntityType,
            entityId: request.EntityId,
            merchantId: request.MerchantId,
            actorType: request.Actor?.Type,
            actorSubject: request.Actor?.Subject,
            actorClientId: request.Actor?.ClientId,
            reason: request.Reason,
            metadata: request.Metadata);

        await _repository.AddAsync(record, cancellationToken);
        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyKeyUniqueConstraintViolationException)
        {
            return await ResolveExistingAsync(idempotencyKey, requestHash, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Concurrent idempotency record was not found after unique constraint conflict.");
        }

        return new CreateAuditRecordResult(record.Id);
    }

    private async Task<CreateAuditRecordResult?> ResolveExistingAsync(
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        FunctionalAuditRecord? existing = await _repository.GetByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);

        return existing is null
            ? null
            : !string.Equals(requestHash, GenerateRequestHash(existing), StringComparison.Ordinal)
            ? throw new ConflictException("Idempotency-Key already used with a different payload.")
            : new CreateAuditRecordResult(existing.Id);
    }

    private static string GenerateRequestHash(CreateAuditRecordCommand request)
        => GenerateRequestHash(
            request.OperationId.ToString(),
            request.CorrelationId?.ToString(),
            request.SourceService,
            request.OperationType,
            request.EntityType,
            request.EntityId,
            request.MerchantId,
            request.Actor?.Type,
            request.Actor?.Subject,
            request.Actor?.ClientId,
            request.Status,
            request.Reason,
            request.Metadata,
            request.OccurredAt);

    private static string GenerateRequestHash(FunctionalAuditRecord record)
        => GenerateRequestHash(
            record.OperationId,
            record.CorrelationId,
            record.SourceService,
            record.OperationType,
            record.EntityType,
            record.EntityId,
            record.MerchantId,
            record.ActorType,
            record.ActorSubject,
            record.ActorClientId,
            record.Status,
            record.Reason,
            record.Metadata,
            record.OccurredAt);

    private static string GenerateRequestHash(
        string operationId,
        string? correlationId,
        string sourceService,
        string operationType,
        string? entityType,
        string? entityId,
        string? merchantId,
        string? actorType,
        string? actorSubject,
        string? actorClientId,
        string status,
        string? reason,
        IReadOnlyDictionary<string, string>? metadata,
        DateTimeOffset occurredAt)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            OperationId = Normalize(operationId),
            CorrelationId = Normalize(correlationId),
            SourceService = Normalize(sourceService),
            OperationType = Normalize(operationType),
            EntityType = Normalize(entityType),
            EntityId = Normalize(entityId),
            MerchantId = Normalize(merchantId),
            ActorType = Normalize(actorType),
            ActorSubject = Normalize(actorSubject),
            ActorClientId = Normalize(actorClientId),
            Status = Normalize(status),
            Reason = Normalize(reason),
            Metadata = Normalize(metadata),
            OccurredAt = occurredAt
        }, JsonOptions);

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string>? Normalize(IReadOnlyDictionary<string, string>? metadata)
        => metadata?.Where(static item => !string.IsNullOrWhiteSpace(item.Key))
                .OrderBy(static item => item.Key, StringComparer.Ordinal)
                .ToDictionary(
                    static item => item.Key.Trim(),
                    static item => item.Value,
                    StringComparer.Ordinal);
}
