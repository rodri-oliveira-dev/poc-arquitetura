using LedgerService.Api.Contracts;
using LedgerService.Api.Mappers;

using FluentAssertions;

namespace LedgerService.UnitTests.Tests;

public sealed class CreateLancamentoInputMapperTests
{
    [Fact]
    public void ToInput_should_normalize_request_payload()
    {
        var request = new CreateLancamentoRequest(
            MerchantId: "m1",
            Type: "  credit ",
            Amount: 10.50m,
            Description: "desc",
            ExternalReference: "ext-1");

        var result = CreateLancamentoInputMapper.ToInput(
            request,
            idempotencyKey: "8b9e5d2b-11f7-4c1f-8ce0-8e4c20d2449b",
            correlationId: "50e1c8fd-0b88-4d89-b855-33d4ff84c5e4");

        result.MerchantId.Should().Be("m1");
        result.Type.Should().Be("CREDIT");
        result.Amount.Should().Be("10.50");
        result.Description.Should().Be("desc");
        result.ExternalReference.Should().Be("ext-1");
        result.IdempotencyKey.Should().Be("8b9e5d2b-11f7-4c1f-8ce0-8e4c20d2449b");
        result.CorrelationId.Should().Be("50e1c8fd-0b88-4d89-b855-33d4ff84c5e4");
    }
}
