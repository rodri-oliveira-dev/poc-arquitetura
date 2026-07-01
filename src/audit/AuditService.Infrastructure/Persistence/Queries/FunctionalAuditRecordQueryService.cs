using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.FunctionalAuditing.ReadModels;
using AuditService.Domain.FunctionalAuditing;

using Microsoft.EntityFrameworkCore;

namespace AuditService.Infrastructure.Persistence.Queries;

public sealed class FunctionalAuditRecordQueryService(AuditDbContext dbContext) : IFunctionalAuditRecordQueryService
{
    public async Task<AuditRecordReadModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        FunctionalAuditRecord? record = await dbContext.FunctionalAuditRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : ToReadModel(record);
    }

    public async Task<IReadOnlyCollection<AuditRecordReadModel>> GetByOperationIdAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationId);

        FunctionalAuditRecord[] records = await dbContext.FunctionalAuditRecords
            .AsNoTracking()
            .Where(x => x.OperationId == operationId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return [.. records.Select(ToReadModel)];
    }

    public async Task<PagedResult<AuditRecordReadModel>> SearchAsync(
        AuditRecordSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        IQueryable<FunctionalAuditRecord> query = dbContext.FunctionalAuditRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(criteria.MerchantId))
            query = query.Where(x => x.MerchantId == criteria.MerchantId);

        if (!string.IsNullOrWhiteSpace(criteria.SourceService))
            query = query.Where(x => x.SourceService == criteria.SourceService);

        if (!string.IsNullOrWhiteSpace(criteria.OperationType))
            query = query.Where(x => x.OperationType == criteria.OperationType);

        if (!string.IsNullOrWhiteSpace(criteria.Status))
            query = query.Where(x => x.Status == criteria.Status);

        if (!string.IsNullOrWhiteSpace(criteria.EntityType))
            query = query.Where(x => x.EntityType == criteria.EntityType);

        if (!string.IsNullOrWhiteSpace(criteria.EntityId))
            query = query.Where(x => x.EntityId == criteria.EntityId);

        query = query.Where(x => x.OccurredAt >= criteria.From && x.OccurredAt <= criteria.To);

        int totalItems = await query.CountAsync(cancellationToken);
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)criteria.PageSize);

        FunctionalAuditRecord[] records = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<AuditRecordReadModel>(
            [.. records.Select(ToReadModel)],
            criteria.Page,
            criteria.PageSize,
            totalItems,
            totalPages);
    }

    private static AuditRecordReadModel ToReadModel(FunctionalAuditRecord record)
    {
        var metadata = record.Metadata.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : record.Metadata.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);

        return new AuditRecordReadModel(
            record.Id,
            record.OperationId,
            record.CorrelationId,
            record.SourceService,
            record.OperationType,
            record.EntityType,
            record.EntityId,
            record.MerchantId,
            record.ActorType is null && record.ActorSubject is null && record.ActorClientId is null
                ? null
                : new AuditRecordActorReadModel(record.ActorType, record.ActorSubject, record.ActorClientId),
            record.Status,
            record.Reason,
            metadata,
            record.OccurredAt,
            record.CreatedAt);
    }
}
