using IdentityService.Domain.Users;
using IdentityService.Infrastructure.Persistence;

namespace IdentityService.UnitTests.Infrastructure.Persistence;

public sealed class SequentialMerchantIdGeneratorTests
{
    [Fact]
    public void Generate_should_return_non_empty_merchant_id_with_expected_prefix_and_max_length()
    {
        var generator = new SequentialMerchantIdGenerator();

        var value = generator.Generate();

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.StartsWith("merchant-", value, StringComparison.Ordinal);
        Assert.True(value.Length <= MerchantId.MaxLength);
        _ = new MerchantId(value);
    }

    [Fact]
    public void Generate_should_avoid_collision_for_consecutive_values()
    {
        var generator = new SequentialMerchantIdGenerator();

        var first = generator.Generate();
        var second = generator.Generate();

        Assert.NotEqual(first, second);
    }
}
