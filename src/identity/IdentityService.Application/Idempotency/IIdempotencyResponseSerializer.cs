namespace IdentityService.Application.Idempotency;

public interface IIdempotencyResponseSerializer
{
    string Serialize<TResponse>(TResponse response);

    TResponse Deserialize<TResponse>(string responseBody);
}
