using AuditService.Application.FunctionalAuditing.ReadModels;

namespace AuditService.Application.Abstractions.Persistence;

public interface IFunctionalAuditRecordQueryService
{
    Task<AuditRecordReadModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditRecordReadModel>> GetByOperationIdAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AuditRecordReadModel>> SearchAsync(
        AuditRecordSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
