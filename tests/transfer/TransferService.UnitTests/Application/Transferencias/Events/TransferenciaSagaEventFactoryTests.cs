using TransferService.Application.Transferencias.Events;
using TransferService.UnitTests.Support;

namespace TransferService.UnitTests.Application.Transferencias.Events;

public sealed class TransferenciaSagaEventFactoryTests
{
    [Fact]
    public void TransferenciaCompensada_should_copy_saga_data_and_trim_correlation()
    {
        var saga = TransferenciaTestData.CreateSaga();

        var evento = TransferenciaSagaEventFactory.TransferenciaCompensada(
            saga,
            "  correlation-1  ",
            TransferenciaTestData.Now);

        Assert.Equal(TransferenciaCompensadaV1.Type, evento.EventType);
        Assert.Equal(saga.Id, evento.TransferenciaId);
        Assert.Equal("merchant-source", evento.SourceMerchantId);
        Assert.Equal("merchant-destination", evento.DestinationMerchantId);
        Assert.Equal(100m, evento.Amount);
        Assert.Equal(TransferenciaTestData.Now, evento.OccurredAt);
        Assert.Equal("correlation-1", evento.CorrelationId);
    }

    [Fact]
    public void TransferenciaFalhou_should_normalize_blank_correlation_to_null()
    {
        var saga = TransferenciaTestData.CreateSaga(correlationId: null);

        var evento = TransferenciaSagaEventFactory.TransferenciaFalhou(
            saga,
            "  ",
            TransferenciaTestData.Now);

        Assert.Equal(TransferenciaFalhouV1.Type, evento.EventType);
        Assert.Null(evento.CorrelationId);
    }

    [Fact]
    public void Factory_methods_should_validate_saga()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TransferenciaSagaEventFactory.TransferenciaCompensada(null!, null, TransferenciaTestData.Now));
    }
}
