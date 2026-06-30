using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Application.Idempotency;

public sealed class Sha256IdempotencyRequestHasher(IIdempotencyResponseSerializer serializer) : IIdempotencyRequestHasher
{
    public string ComputeHash<TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var canonicalPayload = serializer.Serialize(payload);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
