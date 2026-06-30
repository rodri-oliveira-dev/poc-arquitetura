using IdentityService.Application.Users.Ports;

namespace IdentityService.Infrastructure.Persistence;

public sealed class SequentialMerchantIdGenerator : IMerchantIdGenerator
{
    public string Generate()
        => $"merchant-{Guid.NewGuid():N}";
}
