namespace IdentityService.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAt
    {
        get;
    }
}
