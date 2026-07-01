namespace IdentityService.Application.Idempotency;

public interface IIdempotencyRequestHasher
{
    string ComputeHash<TPayload>(TPayload payload);
}
