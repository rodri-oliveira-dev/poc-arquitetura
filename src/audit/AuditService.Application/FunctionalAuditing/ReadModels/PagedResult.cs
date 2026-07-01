namespace AuditService.Application.FunctionalAuditing.ReadModels;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
