using System.Text.Json;

using TransferService.Application.Transferencias.Events;

namespace TransferService.UnitTests.Application.Transferencias.Events;

public sealed class TransferenciaCompensadaV1Tests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Constructor_should_populate_public_properties_and_base_event_type()
    {
        var transferenciaId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);

        var evento = new TransferenciaCompensadaV1(
            transferenciaId,
            "merchant-source",
            "merchant-destination",
            100m,
            occurredAt,
            "correlation-1");

        Assert.Equal(TransferenciaCompensadaV1.Type, evento.EventType);
        Assert.Equal(transferenciaId, evento.TransferenciaId);
        Assert.Equal("merchant-source", evento.SourceMerchantId);
        Assert.Equal("merchant-destination", evento.DestinationMerchantId);
        Assert.Equal(100m, evento.Amount);
        Assert.Equal(occurredAt, evento.OccurredAt);
        Assert.Equal("correlation-1", evento.CorrelationId);
    }

    [Fact]
    public void Serialization_should_preserve_event_contract()
    {
        var transferenciaId = Guid.NewGuid();
        var evento = new TransferenciaCompensadaV1(
            transferenciaId,
            "merchant-source",
            "merchant-destination",
            100m,
            new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero),
            "correlation-1");

        var json = JsonSerializer.Serialize(evento, JsonOptions);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(TransferenciaCompensadaV1.Type, document.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(transferenciaId, document.RootElement.GetProperty("transferenciaId").GetGuid());
        Assert.Equal("merchant-source", document.RootElement.GetProperty("sourceMerchantId").GetString());
        Assert.Equal("merchant-destination", document.RootElement.GetProperty("destinationMerchantId").GetString());
        Assert.Equal(100m, document.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("correlation-1", document.RootElement.GetProperty("correlationId").GetString());
    }
}
