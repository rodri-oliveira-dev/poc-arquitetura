using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Services;

namespace LedgerService.UnitTests.Application.Lancamentos.Services;

public sealed class LedgerEntryCreatedEventFactoryTests
{
    [Fact]
    public void Create_should_map_response_and_correlation_id()
    {
        var response = new LancamentoDto(
            "lan_12345678",
            Guid.NewGuid(),
            "m1",
            "CREDIT",
            "10.00",
            "2026-02-16T00:00:00.0000000Z",
            "desc",
            "ext",
            "2026-02-16T00:00:00.0000000Z");

        var result = LedgerEntryCreatedEventFactory.Create(response, "correlation-id");

        Assert.Equal(response.Id, result.Id);
        Assert.Equal(response.Type, result.Type);
        Assert.Equal(response.Amount, result.Amount);
        Assert.Equal(response.CreatedAt, result.CreatedAt);
        Assert.Equal(response.MerchantId, result.MerchantId);
        Assert.Equal(response.OccurredAt, result.OccurredAt);
        Assert.Equal(response.Description, result.Description);
        Assert.Equal("correlation-id", result.CorrelationId);
        Assert.Equal(response.ExternalReference, result.ExternalReference);
    }
}
