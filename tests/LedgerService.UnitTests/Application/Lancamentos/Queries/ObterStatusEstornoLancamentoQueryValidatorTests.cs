using LedgerService.Application.Lancamentos.Queries;

namespace LedgerService.UnitTests.Application.Lancamentos.Queries;

public sealed class ObterStatusEstornoLancamentoQueryValidatorTests
{
    private readonly ObterStatusEstornoLancamentoQueryValidator _validator = new();

    [Fact]
    public void Should_reject_empty_estorno_id()
    {
        var query = new ObterStatusEstornoLancamentoQuery(Guid.Empty, ["m1"]);

        var result = _validator.Validate(query);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ObterStatusEstornoLancamentoQuery.EstornoId));
    }

    [Fact]
    public void Should_reject_missing_authorized_merchants()
    {
        var query = new ObterStatusEstornoLancamentoQuery(Guid.NewGuid(), []);

        var result = _validator.Validate(query);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ObterStatusEstornoLancamentoQuery.AuthorizedMerchantIds));
    }

    [Fact]
    public void Should_accept_valid_query()
    {
        var query = new ObterStatusEstornoLancamentoQuery(Guid.NewGuid(), ["m1"]);

        var result = _validator.Validate(query);
        Assert.True(result.IsValid);
    }
}
