using ApiDefaults.Middlewares;

using FluentValidation;

using Microsoft.AspNetCore.Http;

using TransferService.Api.Contracts.Requests;
using TransferService.Api.Controllers.Binds;

namespace TransferService.IntegrationTests.Api.Controllers.Binds;

public sealed class SolicitarTransferenciaBindTests
{
    [Fact]
    public void Bind_should_create_command_from_request_and_explicit_correlation_id()
    {
        var context = new DefaultHttpContext();
        var request = CreateRequest();
        var idempotencyKey = Guid.NewGuid().ToString();

        var command = SolicitarTransferenciaBind.Bind(context, idempotencyKey, "correlation-explicit", request);

        Assert.Equal(idempotencyKey, command.IdempotencyKey);
        Assert.Equal("m1", command.SourceMerchantId);
        Assert.Equal("m2", command.DestinationMerchantId);
        Assert.Equal(100m, command.Amount);
        Assert.Equal("correlation-explicit", command.CorrelationId);
        Assert.Equal("Transferencia entre carteiras", command.Description);
        Assert.Equal("pedido-123", command.ExternalReference);
    }

    [Fact]
    public void Bind_should_use_correlation_header_when_argument_is_blank()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "correlation-header";

        var command = SolicitarTransferenciaBind.Bind(
            context,
            Guid.NewGuid().ToString(),
            " ",
            CreateRequest());

        Assert.Equal("correlation-header", command.CorrelationId);
    }

    [Fact]
    public void Bind_should_validate_transport_and_body_arguments()
    {
        var context = new DefaultHttpContext();

        Assert.Throws<ArgumentNullException>(() => SolicitarTransferenciaBind.Bind(null!, Guid.NewGuid().ToString(), null, CreateRequest()));
        Assert.Throws<ValidationException>(() => SolicitarTransferenciaBind.Bind(context, "", null, CreateRequest()));
        Assert.Throws<ValidationException>(() => SolicitarTransferenciaBind.Bind(context, "not-a-guid", null, CreateRequest()));
        Assert.Throws<ValidationException>(() => SolicitarTransferenciaBind.Bind(context, Guid.NewGuid().ToString(), null, null));
        Assert.Throws<ValidationException>(() => SolicitarTransferenciaBind.Bind(context, Guid.NewGuid().ToString(), null, CreateRequest(amount: null)));
    }

    private static SolicitarTransferenciaRequest CreateRequest(decimal? amount = 100m)
        => new()
        {
            SourceMerchantId = "m1",
            DestinationMerchantId = "m2",
            Amount = amount,
            Description = "Transferencia entre carteiras",
            ExternalReference = "pedido-123"
        };
}
