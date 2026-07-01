using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Balances.Commands;
using BalanceService.Application.Balances.Replay;
using BalanceService.Application.Contracts.Events;

using MediatR;

using Microsoft.Extensions.Logging;

using Moq;

namespace BalanceService.Application.Tests.Application.Balances.Replay;

public sealed class ManualEventReplayHandlerTests
{
    private const string ValidPayload = """
        {
          "id": "lan_1a2b3c4d",
          "type": "CREDIT",
          "amount": "150.00",
          "currency": "BRL",
          "createdAt": "2026-06-06T12:34:56.0000000Z",
          "merchantId": "merchant-001",
          "occurredAt": "2026-06-06T12:34:56.0000000Z",
          "description": "Venda aprovada",
          "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
          "externalReference": "order-123"
        }
        """;

    private readonly Mock<IProcessedEventRepository> _processedEvents = new(MockBehavior.Strict);
    private readonly Mock<ISender> _sender = new(MockBehavior.Strict);
    private readonly Mock<ILogger<ManualEventReplayHandler>> _logger = new();
    private readonly ManualEventReplayHandler _sut;

    public ManualEventReplayHandlerTests()
    {
        var validator = new JsonSchemaEventContractValidator(new EmbeddedEventContractSchemaCatalog());
        var evaluator = new EventReplayMessageEvaluator(validator, _processedEvents.Object);
        _sut = new ManualEventReplayHandler(
            evaluator,
            _sender.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Should_replay_valid_message()
    {
        _processedEvents
            .Setup(x => x.ExistsAsync("lan_1a2b3c4d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ApplyLedgerEntryCreatedCommand? sentCommand = null;
        _sender
            .Setup(x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ApplyLedgerEntryCreatedResult>, CancellationToken>((request, _) =>
                sentCommand = Assert.IsType<ApplyLedgerEntryCreatedCommand>(request))
            .ReturnsAsync(ApplyLedgerEntryCreatedResult.Processed);

        ManualEventReplayResult result = await _sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(ManualEventReplayStatus.Replayed, result.Result);
        Assert.Equal("lan_1a2b3c4d", result.EventId);
        Assert.False(string.IsNullOrWhiteSpace(result.ReplayId));
        Assert.NotNull(sentCommand);
        Assert.Equal("LedgerEntryCreated.v2", sentCommand!.EventType);
        Assert.Equal("BRL", sentCommand.Event.Currency);
        _processedEvents.VerifyAll();
        _sender.VerifyAll();
    }

    [Fact]
    public async Task Should_skip_message_already_processed()
    {
        _processedEvents
            .Setup(x => x.ExistsAsync("lan_1a2b3c4d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ManualEventReplayResult result = await _sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(ManualEventReplayStatus.SkippedAlreadyProcessed, result.Result);
        Assert.Equal("lan_1a2b3c4d", result.EventId);
        _sender.Verify(
            x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _processedEvents.VerifyAll();
    }

    [Fact]
    public async Task Should_reject_invalid_payload()
    {
        const string invalidPayload = """
            {
              "id": "lan_1a2b3c4d",
              "type": "CREDIT",
              "amount": "-150.00",
              "currency": "BRL",
              "createdAt": "2026-06-06T12:34:56.0000000Z",
              "merchantId": "merchant-001",
              "occurredAt": "2026-06-06T12:34:56.0000000Z",
              "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
            }
            """;

        ManualEventReplayResult result = await _sut.Handle(CreateCommand(payload: invalidPayload), CancellationToken.None);

        Assert.Equal(ManualEventReplayStatus.RejectedInvalidContract, result.Result);
        Assert.Null(result.EventId);
        Assert.Contains("amount", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _processedEvents.VerifyNoOtherCalls();
        _sender.Verify(
            x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_reject_unknown_version()
    {
        ManualEventReplayResult result = await _sut.Handle(CreateCommand(eventVersion: "v99"), CancellationToken.None);

        Assert.Equal(ManualEventReplayStatus.RejectedUnsupportedVersion, result.Result);
        Assert.Null(result.EventId);
        Assert.Contains("v99", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _processedEvents.VerifyNoOtherCalls();
        _sender.Verify(
            x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_report_processing_error()
    {
        _processedEvents
            .Setup(x => x.ExistsAsync("lan_1a2b3c4d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sender
            .Setup(x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        ManualEventReplayResult result = await _sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(ManualEventReplayStatus.FailedProcessing, result.Result);
        Assert.Equal("lan_1a2b3c4d", result.EventId);
        Assert.Contains("database unavailable", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _processedEvents.VerifyAll();
        _sender.VerifyAll();
    }

    private static ManualEventReplayCommand CreateCommand(
        string payload = ValidPayload,
        string eventVersion = "v2")
        => new(
            payload,
            "LedgerEntryCreated",
            eventVersion,
            "PubSub",
            new Dictionary<string, string>
            {
                ["source"] = "balance-ledger-events-dlq-sub"
            },
            "operational replay after DLQ investigation");
}
