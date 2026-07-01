using AuditService.Domain.Exceptions;

namespace AuditService.Domain.FunctionalAuditing;

public sealed class FunctionalAuditRecord
{
    public const int SourceServiceMaxLength = 100;
    public const int OperationTypeMaxLength = 150;
    public const int ReasonMaxLength = 1_000;

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Received",
        "Succeeded",
        "Failed",
        "Rejected",
        "Replayed"
    };

    private static readonly HashSet<string> ValidActorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User",
        "Client",
        "System"
    };

    private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);

    private FunctionalAuditRecord()
    {
        OperationId = string.Empty;
        SourceService = string.Empty;
        OperationType = string.Empty;
        Status = string.Empty;
    }

    private FunctionalAuditRecord(
        Guid id,
        string operationId,
        string? correlationId,
        string? idempotencyKey,
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
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        OperationId = Required(operationId, nameof(OperationId));
        CorrelationId = Optional(correlationId);
        IdempotencyKey = Optional(idempotencyKey);
        SourceService = RequiredLimited(sourceService, nameof(SourceService), SourceServiceMaxLength);
        OperationType = RequiredLimited(operationType, nameof(OperationType), OperationTypeMaxLength);
        EntityType = Optional(entityType);
        EntityId = Optional(entityId);
        MerchantId = Optional(merchantId);
        ActorType = ValidateActorType(actorType);
        ActorSubject = Optional(actorSubject);
        ActorClientId = Optional(actorClientId);
        Status = ValidateStatus(status);
        Reason = OptionalLimited(reason, nameof(Reason), ReasonMaxLength);
        OccurredAt = occurredAt == default
            ? throw new DomainException("OccurredAt e obrigatorio.")
            : occurredAt;
        CreatedAt = createdAt == default
            ? throw new DomainException("CreatedAt e obrigatorio.")
            : createdAt;

        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                if (!string.IsNullOrWhiteSpace(item.Key))
                    _metadata[item.Key] = item.Value;
            }
        }
    }

    public Guid Id
    {
        get; private set;
    }

    public string OperationId
    {
        get; private set;
    }

    public string? CorrelationId
    {
        get; private set;
    }

    public string? IdempotencyKey
    {
        get; private set;
    }

    public string SourceService
    {
        get; private set;
    }

    public string OperationType
    {
        get; private set;
    }

    public string? EntityType
    {
        get; private set;
    }

    public string? EntityId
    {
        get; private set;
    }

    public string? MerchantId
    {
        get; private set;
    }

    public string? ActorType
    {
        get; private set;
    }

    public string? ActorSubject
    {
        get; private set;
    }

    public string? ActorClientId
    {
        get; private set;
    }

    public string Status
    {
        get; private set;
    }

    public string? Reason
    {
        get; private set;
    }

    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    public DateTimeOffset OccurredAt
    {
        get; private set;
    }

    public DateTimeOffset CreatedAt
    {
        get; private set;
    }

    public static FunctionalAuditRecord Create(
        string operationId,
        string sourceService,
        string operationType,
        string status,
        DateTimeOffset occurredAt,
        string? correlationId = null,
        string? idempotencyKey = null,
        string? entityType = null,
        string? entityId = null,
        string? merchantId = null,
        string? actorType = null,
        string? actorSubject = null,
        string? actorClientId = null,
        string? reason = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        DateTimeOffset? createdAt = null)
        => new(
            Guid.NewGuid(),
            operationId,
            correlationId,
            idempotencyKey,
            sourceService,
            operationType,
            entityType,
            entityId,
            merchantId,
            actorType,
            actorSubject,
            actorClientId,
            status,
            reason,
            metadata,
            occurredAt,
            createdAt ?? DateTimeOffset.UtcNow);

    private static string Required(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new DomainException($"{name} e obrigatorio.")
            : value.Trim();

    private static string RequiredLimited(string value, string name, int maxLength)
    {
        var normalized = Required(value, name);
        return normalized.Length > maxLength
            ? throw new DomainException($"{name} deve ter no maximo {maxLength} caracteres.")
            : normalized;
    }

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? OptionalLimited(string? value, string name, int maxLength)
    {
        var normalized = Optional(value);
        return normalized is not null && normalized.Length > maxLength
            ? throw new DomainException($"{name} deve ter no maximo {maxLength} caracteres.")
            : normalized;
    }

    private static string ValidateStatus(string status)
    {
        var normalized = Required(status, nameof(Status));
        return ValidStatuses.Contains(normalized)
            ? normalized
            : throw new DomainException($"Status '{normalized}' nao e suportado para auditoria funcional.");
    }

    private static string? ValidateActorType(string? actorType)
    {
        var normalized = Optional(actorType);
        return normalized is null || ValidActorTypes.Contains(normalized)
            ? normalized
            : throw new DomainException($"ActorType '{normalized}' nao e suportado para auditoria funcional.");
    }
}
