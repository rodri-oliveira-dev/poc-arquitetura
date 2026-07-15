using System.Security.Claims;

using ApiDefaults.Security;

namespace ApiDefaults.Tests.Security;

public sealed class MerchantClaimsTests
{
    [Fact]
    public void AuthorizedMerchantIds_should_return_empty_for_principal_without_identities()
    {
        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(new ClaimsPrincipal());

        Assert.Empty(result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_return_empty_for_unauthenticated_identity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(MerchantClaims.ClaimType, "merchant-1")]));

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Empty(result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_return_empty_when_claim_is_missing()
    {
        var principal = Principal([new Claim("sub", "user-1")]);

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Empty(result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_split_space_separated_claims_and_keep_first_seen_order()
    {
        var principal = Principal(
        [
            new Claim(MerchantClaims.ClaimType, "merchant-1 merchant-2"),
            new Claim(MerchantClaims.ClaimType, "merchant-2 merchant-3")
        ]);

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Equal(["merchant-1", "merchant-2", "merchant-3"], result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_ignore_empty_values()
    {
        var principal = Principal([new Claim(MerchantClaims.ClaimType, "   ")]);

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Empty(result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_ignore_alternative_claim_type()
    {
        var principal = Principal([new Claim("merchant", "merchant-1")]);

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Empty(result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_preserve_case_sensitivity()
    {
        var principal = Principal(
        [
            new Claim(MerchantClaims.ClaimType, "Merchant-1"),
            new Claim(MerchantClaims.ClaimType, "merchant-1")
        ]);

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Equal(["Merchant-1", "merchant-1"], result);
    }

    [Fact]
    public void AuthorizedMerchantIds_should_return_large_and_punctuation_values_without_type_conversion()
    {
        string largeMerchantId = new('a', 1024);
        var principal = Principal(
        [
            new Claim(MerchantClaims.ClaimType, "merchant:1"),
            new Claim(MerchantClaims.ClaimType, largeMerchantId)
        ]);

        IReadOnlyCollection<string> result = MerchantClaims.AuthorizedMerchantIds(principal);

        Assert.Equal(["merchant:1", largeMerchantId], result);
    }

    [Fact]
    public void AllowsMerchant_should_return_true_for_exact_authorized_merchant()
    {
        var principal = Principal([new Claim(MerchantClaims.ClaimType, "merchant-1 merchant-2")]);

        bool result = MerchantClaims.AllowsMerchant(principal, " merchant-2 ");

        Assert.True(result);
    }

    [Theory]
    [InlineData("merchant-2")]
    [InlineData("merchant-1-all")]
    [InlineData("")]
    [InlineData("   ")]
    public void AllowsMerchant_should_return_false_when_merchant_is_not_exactly_authorized(string merchantId)
    {
        var principal = Principal([new Claim(MerchantClaims.ClaimType, "merchant-1")]);

        bool result = MerchantClaims.AllowsMerchant(principal, merchantId);

        Assert.False(result);
    }

    [Fact]
    public void AllowsMerchant_should_throw_for_null_principal()
    {
        Assert.Throws<ArgumentNullException>(() => MerchantClaims.AllowsMerchant(null!, "merchant-1"));
    }

    [Fact]
    public void AuthorizedMerchantIds_should_throw_for_null_principal()
    {
        Assert.Throws<ArgumentNullException>(() => MerchantClaims.AuthorizedMerchantIds(null!));
    }

    private static ClaimsPrincipal Principal(IEnumerable<Claim> claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
