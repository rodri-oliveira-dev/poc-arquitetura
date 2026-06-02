using System.Text.Json;

using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.Processors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LedgerService.Worker.Tests.Messaging.Processors;

public sealed class ReprocessamentoLancamentosMessageProcessorTests
{
    [Fact]
    public async Task Should_process_reprocessamento_message_and_send_command()
    {
        ProcessarReprocessamentoLancamentosCommand? command = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((request, _) => command = request as ProcessarReprocessamentoLancamentosCommand)
            .Returns(Task.CompletedTask);
        var sut = CreateSut(sender.Object);
        var reprocessamentoId = Guid.NewGuid();

        var shouldCommit = await sut.ProcessAsync(
            CreateMessage(ValidPayload(reprocessamentoId)),
            CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.NotNull(command);
        Assert.Equal(reprocessamentoId, command!.ReprocessamentoId);
    }

    [Fact]
    public async Task Should_ignore_invalid_payload_and_allow_commit()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);

        var shouldCommit = await sut.ProcessAsync(
            CreateMessage("{invalid-json"),
            CancellationToken.None);
        Assert.True(shouldCommit);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_ignore_unsupported_event_type_and_allow_commit()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);
        var shouldCommit = await sut.ProcessAsync(
            CreateMessage(ValidPayload(Guid.NewGuid()), eventType: LedgerEntryCreatedV1.EventType),
            CancellationToken.None);
        Assert.True(shouldCommit);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_validate_reprocessamento_topic()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);

        var message = CreateMessage(
            ValidPayload(Guid.NewGuid()),
            "ledger.ledgerentry.created");

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_ignore_missing_event_type_and_allow_commit()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);

        var shouldCommit = await sut.ProcessAsync(
            CreateMessage(ValidPayload(Guid.NewGuid()), eventType: string.Empty),
            CancellationToken.None);

        Assert.True(shouldCommit);
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "m1", "42")]
    [InlineData("17f05de3-09ec-44ef-a971-0114f214116e", "", "42")]
    [InlineData("17f05de3-09ec-44ef-a971-0114f214116e", "m1", "invalid")]
    public async Task Should_ignore_invalid_required_payload_fields_and_allow_commit(
        string reprocessamentoId,
        string merchantId,
        string correlationId)
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);
        var payload = ValidPayload(Guid.Parse(reprocessamentoId), merchantId, correlationId);

        var shouldCommit = await sut.ProcessAsync(CreateMessage(payload), CancellationToken.None);

        Assert.True(shouldCommit);
        sender.VerifyNoOtherCalls();
    }

    private static ReprocessamentoLancamentosMessageProcessor CreateSut(ISender sender)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sender);

        return new ReprocessamentoLancamentosMessageProcessor(
            services.BuildServiceProvider(),
            NullLogger<ReprocessamentoLancamentosMessageProcessor>.Instance);
    }

    private static ReceivedMessage CreateMessage(
        string payload,
        string source = ReprocessamentoLancamentosMessageProcessor.SourceName,
        string eventType = ReprocessamentoLancamentosSolicitadoV1.EventType)
        => new(
            payload,
            eventType,
            null,
            null,
            null,
            null,
            null,
            "key",
            new Dictionary<string, string>(),
            new TransportMessageContext("kafka", source, "0", "42", null, new Dictionary<string, string>()));

    private static string ValidPayload(
        Guid reprocessamentoId,
        string merchantId = "m1",
        string? correlationId = null)
        => JsonSerializer.Serialize(new ReprocessamentoLancamentosSolicitadoV1(
            reprocessamentoId,
            merchantId,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            "Pending",
            "2026-05-07T10:00:00.0000000",
            correlationId ?? Guid.NewGuid().ToString()));
}
