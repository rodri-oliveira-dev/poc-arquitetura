using System.Text.Json;

using FluentAssertions;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using Moq;

namespace LedgerService.UnitTests.Application.Lancamentos.Commands;

public sealed class SolicitarReprocessamentoLancamentosHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_create_pending_reprocessamento_and_outbox_message()
    {
        var reprocessamentoRepo = new Mock<IReprocessamentoLancamentosRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);
        var command = ValidCommand();

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(command.MerchantId, command.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);

        ReprocessamentoLancamentos? createdReprocessamento = null;
        reprocessamentoRepo.Setup(x => x.AddAsync(It.IsAny<ReprocessamentoLancamentos>(), It.IsAny<CancellationToken>()))
            .Callback<ReprocessamentoLancamentos, CancellationToken>((r, _) => createdReprocessamento = r)
            .Returns(Task.CompletedTask);

        IdempotencyRecord? createdIdem = null;
        idemRepo.Setup(x => x.AddAsync(It.IsAny<IdempotencyRecord>(), It.IsAny<CancellationToken>()))
            .Callback<IdempotencyRecord, CancellationToken>((r, _) => createdIdem = r)
            .Returns(Task.CompletedTask);

        OutboxMessage? createdOutbox = null;
        outboxRepo.Setup(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((m, _) => createdOutbox = m)
            .Returns(Task.CompletedTask);

        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(reprocessamentoRepo, idemRepo, outboxRepo, uow);

        var result = await sut.Handle(command, CancellationToken.None);

        result.ReprocessamentoId.Should().NotBeEmpty();
        result.MerchantId.Should().Be(command.MerchantId);
        result.DataInicial.Should().Be(command.DataInicial);
        result.DataFinal.Should().Be(command.DataFinal);
        result.Status.Should().Be(ReprocessamentoLancamentosStatus.Pending.ToString());
        result.StatusUrl.Should().Be($"/api/v1/lancamentos/reprocessamentos/{result.ReprocessamentoId}");

        createdReprocessamento.Should().NotBeNull();
        createdReprocessamento!.Status.Should().Be(ReprocessamentoLancamentosStatus.Pending);
        createdReprocessamento.Motivo.Should().Be(command.Motivo);
        createdReprocessamento.DataInicial.Should().Be(command.DataInicial);
        createdReprocessamento.DataFinal.Should().Be(command.DataFinal);

        createdIdem.Should().NotBeNull();
        createdIdem!.ResponseStatusCode.Should().Be(202);
        createdIdem.LedgerEntryId.Should().Be(result.ReprocessamentoId);
        createdIdem.ResponseBody.Should().Contain(result.ReprocessamentoId.ToString());

        createdOutbox.Should().NotBeNull();
        createdOutbox!.AggregateType.Should().Be("ReprocessamentoLancamentos");
        createdOutbox.AggregateId.Should().Be(result.ReprocessamentoId);
        createdOutbox.EventType.Should().Be(ReprocessamentoLancamentosSolicitadoV1.EventType);
        createdOutbox.CorrelationId.Should().Be(Guid.Parse(command.CorrelationId));

        var outboxEvent = JsonSerializer.Deserialize<ReprocessamentoLancamentosSolicitadoV1>(
            createdOutbox.Payload,
            JsonOptions);
        outboxEvent.Should().NotBeNull();
        outboxEvent!.ReprocessamentoId.Should().Be(result.ReprocessamentoId);
        outboxEvent.Status.Should().Be("Pending");
        outboxEvent.Motivo.Should().Be(command.Motivo);
    }

    [Fact]
    public async Task Should_replay_idempotent_response()
    {
        var reprocessamentoRepo = new Mock<IReprocessamentoLancamentosRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);
        var command = ValidCommand();
        var reprocessamentoId = Guid.NewGuid();
        var expected = new SolicitarReprocessamentoLancamentosResult(
            reprocessamentoId,
            command.MerchantId,
            command.DataInicial,
            command.DataFinal,
            "Pending",
            $"/api/v1/lancamentos/reprocessamentos/{reprocessamentoId}");

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(command.MerchantId, command.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecord(
                command.MerchantId,
                command.IdempotencyKey,
                ComputeRequestHash(command),
                expected.ReprocessamentoId,
                202,
                JsonSerializer.Serialize(expected, JsonOptions),
                DateTime.Now.AddDays(7)));
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(reprocessamentoRepo, idemRepo, outboxRepo, uow);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().Be(expected);
        reprocessamentoRepo.VerifyNoOtherCalls();
        outboxRepo.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_throw_forbidden_when_merchant_is_not_authorized()
    {
        var reprocessamentoRepo = new Mock<IReprocessamentoLancamentosRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var command = ValidCommand() with { AuthorizedMerchantIds = ["m2"] };
        var sut = CreateSut(reprocessamentoRepo, idemRepo, outboxRepo, uow);

        var act = async () => await sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        uow.VerifyNoOtherCalls();
    }

    private static SolicitarReprocessamentoLancamentosHandler CreateSut(
        Mock<IReprocessamentoLancamentosRepository> reprocessamentoRepo,
        Mock<IIdempotencyRecordRepository> idemRepo,
        Mock<IOutboxMessageRepository> outboxRepo,
        Mock<IUnitOfWork> uow)
        => new(reprocessamentoRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

    private static SolicitarReprocessamentoLancamentosCommand ValidCommand()
        => new(
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            ["m1"]);

    private static string ComputeRequestHash(SolicitarReprocessamentoLancamentosCommand command)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            MerchantId = command.MerchantId.Trim(),
            command.DataInicial,
            command.DataFinal,
            Motivo = command.Motivo.Trim()
        }, JsonOptions);

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
