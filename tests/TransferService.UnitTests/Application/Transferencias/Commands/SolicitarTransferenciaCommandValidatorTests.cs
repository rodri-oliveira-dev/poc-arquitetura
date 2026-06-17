using TransferService.Application.Transferencias.Commands;

namespace TransferService.UnitTests.Application.Transferencias.Commands;

public sealed class SolicitarTransferenciaCommandValidatorTests
{
    private readonly SolicitarTransferenciaCommandValidator _validator = new();

    [Fact]
    public void Validate_should_accept_valid_command()
    {
        var command = ValidCommand();

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_should_reject_empty_idempotency_key()
    {
        var command = ValidCommand() with { IdempotencyKey = " " };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(SolicitarTransferenciaCommand.IdempotencyKey));
    }

    [Fact]
    public void Validate_should_reject_same_merchants()
    {
        var command = ValidCommand() with
        {
            SourceMerchantId = "merchant-1",
            DestinationMerchantId = " merchant-1 "
        };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.ErrorMessage.Contains("nao pode ser igual", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.123)]
    public void Validate_should_reject_invalid_amount(decimal amount)
    {
        var command = ValidCommand() with { Amount = amount };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(SolicitarTransferenciaCommand.Amount));
    }

    private static SolicitarTransferenciaCommand ValidCommand()
        => new(
            "idem-1",
            "merchant-source",
            "merchant-destination",
            100.50m,
            "correlation-1");
}
