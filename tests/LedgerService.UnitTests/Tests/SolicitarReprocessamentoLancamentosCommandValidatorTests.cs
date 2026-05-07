using FluentAssertions;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.UnitTests.Tests;

public sealed class SolicitarReprocessamentoLancamentosCommandValidatorTests
{
    private readonly SolicitarReprocessamentoLancamentosCommandValidator _validator = new();

    [Fact]
    public void Should_accept_valid_command()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curto")]
    public void Should_reject_invalid_motivo(string motivo)
    {
        var command = ValidCommand() with { Motivo = motivo };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarReprocessamentoLancamentosCommand.Motivo));
    }

    [Fact]
    public void Should_reject_invalid_period()
    {
        var command = ValidCommand() with
        {
            DataInicial = new DateOnly(2026, 5, 6),
            DataFinal = new DateOnly(2026, 5, 1)
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarReprocessamentoLancamentosCommand.DataFinal));
    }

    [Fact]
    public void Should_reject_period_above_operational_limit()
    {
        var command = ValidCommand() with
        {
            DataInicial = new DateOnly(2026, 5, 1),
            DataFinal = new DateOnly(2026, 6, 1)
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarReprocessamentoLancamentosCommand.DataFinal));
    }

    [Fact]
    public void Should_reject_invalid_idempotency_key()
    {
        var command = ValidCommand() with { IdempotencyKey = "invalid" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarReprocessamentoLancamentosCommand.IdempotencyKey));
    }

    private static SolicitarReprocessamentoLancamentosCommand ValidCommand()
        => new(
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            ["m1"]);
}
