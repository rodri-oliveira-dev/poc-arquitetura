using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Api.Mappers;


namespace LedgerService.UnitTests.Api.Mappers;

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
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal("CREDIT", result.Type);
        Assert.Equal("10.50", result.Amount);
        Assert.Equal("desc", result.Description);
        Assert.Equal("ext-1", result.ExternalReference);
        Assert.Equal("8b9e5d2b-11f7-4c1f-8ce0-8e4c20d2449b", result.IdempotencyKey);
        Assert.Equal("50e1c8fd-0b88-4d89-b855-33d4ff84c5e4", result.CorrelationId);
    }
}
