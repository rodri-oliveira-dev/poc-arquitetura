using FluentAssertions;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.UnitTests.Application.Lancamentos.Commands;

public sealed class SolicitarEstornoLancamentoCommandValidatorTests
{
    private readonly SolicitarEstornoLancamentoCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curto")]
    public void Should_reject_invalid_motivo(string motivo)
    {
        var command = ValidCommand() with { Motivo = motivo };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarEstornoLancamentoCommand.Motivo));
    }

    [Fact]
    public void Should_reject_empty_lancamento_id()
    {
        var command = ValidCommand() with { LancamentoId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarEstornoLancamentoCommand.LancamentoId));
    }

    [Fact]
    public void Should_accept_valid_command()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    private static SolicitarEstornoLancamentoCommand ValidCommand()
        => new(
            Guid.NewGuid(),
            "Erro operacional no lancamento original",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            ["m1"]);
}
