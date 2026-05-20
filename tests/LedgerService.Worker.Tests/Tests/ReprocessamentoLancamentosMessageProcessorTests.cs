using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using FluentAssertions;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Infrastructure.Reprocessamentos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LedgerService.Worker.Tests.Tests;

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
            CreateResult(ValidPayload(reprocessamentoId), HeadersWithEventType()),
            CancellationToken.None);

        shouldCommit.Should().BeTrue();
        command.Should().NotBeNull();
        command!.ReprocessamentoId.Should().Be(reprocessamentoId);
    }

    [Fact]
    public async Task Should_ignore_invalid_payload_and_allow_commit()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);

        var shouldCommit = await sut.ProcessAsync(
            CreateResult("{invalid-json", HeadersWithEventType()),
            CancellationToken.None);

        shouldCommit.Should().BeTrue();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_ignore_unsupported_event_type_and_allow_commit()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);
        var headers = new Headers
        {
            { "event_type", Encoding.UTF8.GetBytes(LedgerEntryCreatedV1.EventType) }
        };

        var shouldCommit = await sut.ProcessAsync(
            CreateResult(ValidPayload(Guid.NewGuid()), headers),
            CancellationToken.None);

        shouldCommit.Should().BeTrue();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_validate_reprocessamento_topic()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(sender.Object);

        var result = CreateResult(
            ValidPayload(Guid.NewGuid()),
            HeadersWithEventType(),
            "ledger.ledgerentry.created");

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);

        shouldCommit.Should().BeTrue();
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

    private static ConsumeResult<string, string> CreateResult(
        string payload,
        Headers headers,
        string topic = ReprocessamentoLancamentosMessageProcessor.TopicName)
        => new()
        {
            Topic = topic,
            Partition = new Partition(0),
            Offset = new Offset(42),
            Message = new Message<string, string>
            {
                Key = "key",
                Value = payload,
                Headers = headers
            }
        };

    private static Headers HeadersWithEventType()
        => new()
        {
            { "event_type", Encoding.UTF8.GetBytes(ReprocessamentoLancamentosSolicitadoV1.EventType) }
        };

    private static string ValidPayload(Guid reprocessamentoId)
        => JsonSerializer.Serialize(new ReprocessamentoLancamentosSolicitadoV1(
            reprocessamentoId,
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            "Pending",
            "2026-05-07T10:00:00.0000000",
            Guid.NewGuid().ToString()));
}
