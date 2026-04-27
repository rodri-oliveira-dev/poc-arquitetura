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

    [Fact]
    public void Should_be_invalid_when_merchant_exceeds_maximum_length()
    {
        var input = LancamentoFixture.ValidInput(merchantId: new string('m', 101));

        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.MerchantId));
    }

    [Theory]
    [InlineData("10.123")]
    [InlineData("1234567890123456789.00")]
    public void Should_be_invalid_when_amount_exceeds_decimal_constraints(string amount)
    {
        var input = LancamentoFixture.ValidInput(amount: amount);

        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.Amount));
    }

    [Theory]
    [InlineData("100.01", "CREDIT")]
    [InlineData("-100.01", "DEBIT")]
    [InlineData("1234567890123456.78", "CREDIT")]
    public void Should_be_valid_when_amount_respects_decimal_constraints(string amount, string type)
    {
        var input = LancamentoFixture.ValidInput(type: type, amount: amount);

        var result = _validator.Validate(input);

        result.IsValid.Should().BeTrue();
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
    public void Should_ignore_transport_headers_in_request_validator()
    {
        var input = LancamentoFixture.ValidInput(
            idempotencyKey: "not-a-guid",
            correlationId: "not-a-guid");

        var result = _validator.Validate(input);

        result.IsValid.Should().BeTrue();
    }
}
