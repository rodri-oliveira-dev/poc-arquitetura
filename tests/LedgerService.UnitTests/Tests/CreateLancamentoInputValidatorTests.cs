using FluentAssertions;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using LedgerService.UnitTests.Fixtures;

namespace LedgerService.UnitTests.Tests;

public sealed class CreateLancamentoInputValidatorTests
{
    private readonly CreateLancamentoInputValidator _validator = new();

    [Fact]
    public void Should_be_valid_for_credit_with_positive_amount_and_uppercase_type()
    {
        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");

        var result = _validator.Validate(input);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("CREDIT", "-1")]
    [InlineData("DEBIT", "1")]
    [InlineData("CREDIT", "0")]
    [InlineData("DEBIT", "0")]
    [InlineData("CREDIT", "abc")]
    public void Should_be_invalid_when_amount_breaks_business_rule(string type, string amount)
    {
        var input = LancamentoFixture.ValidInput(type: type, amount: amount);

        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_be_invalid_when_merchant_is_empty(string merchantId)
    {
        var input = LancamentoFixture.ValidInput(merchantId: merchantId);

        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.MerchantId));
    }

    [Theory]
    [InlineData("credit")]
    [InlineData(" debit ")]
    [InlineData("X")]
    public void Should_be_invalid_when_type_is_not_canonical(string type)
    {
        // Nota: a API normaliza no Bind. Este validator exige canônico.
        var input = LancamentoFixture.ValidInput(type: type, amount: "10");

        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.Type));
    }

    [Fact]
    public void Should_be_invalid_when_idempotency_key_is_not_guid()
    {
        var input = LancamentoFixture.ValidInput(idempotencyKey: "not-a-guid");
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.IdempotencyKey));
    }

    [Fact]
    public void Should_be_invalid_when_correlation_id_is_not_guid()
    {
        var input = LancamentoFixture.ValidInput(correlationId: "not-a-guid");
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.CorrelationId));
    }
}
