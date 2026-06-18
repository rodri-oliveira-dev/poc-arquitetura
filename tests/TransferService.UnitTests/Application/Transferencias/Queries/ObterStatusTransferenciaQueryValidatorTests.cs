using TransferService.Application.Transferencias.Queries;

namespace TransferService.UnitTests.Application.Transferencias.Queries;

public sealed class ObterStatusTransferenciaQueryValidatorTests
{
    private readonly ObterStatusTransferenciaQueryValidator _validator = new();

    [Fact]
    public void Validate_should_accept_valid_query()
    {
        var query = new ObterStatusTransferenciaQuery(Guid.NewGuid(), ["merchant-source"]);

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_should_reject_empty_transferencia_id()
    {
        var query = new ObterStatusTransferenciaQuery(Guid.Empty, ["merchant-source"]);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(ObterStatusTransferenciaQuery.TransferenciaId));
    }

    [Fact]
    public void Validate_should_reject_null_authorized_merchants()
    {
        var query = new ObterStatusTransferenciaQuery(Guid.NewGuid(), null!);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(ObterStatusTransferenciaQuery.AuthorizedMerchantIds));
    }
}
