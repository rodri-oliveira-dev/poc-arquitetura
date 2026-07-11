using System.Text.Json;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Idempotency;
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
        Assert.NotEqual(Guid.Empty, result.ReprocessamentoId);
        Assert.Equal(command.MerchantId, result.MerchantId);
        Assert.Equal(command.DataInicial, result.DataInicial);
        Assert.Equal(command.DataFinal, result.DataFinal);
        Assert.Equal(ReprocessamentoLancamentosStatus.Pending.ToString(), result.Status);
        Assert.Equal($"/api/v1/lancamentos/reprocessamentos/{result.ReprocessamentoId}", result.StatusUrl);
        Assert.NotNull(createdReprocessamento);
        Assert.Equal(ReprocessamentoLancamentosStatus.Pending, createdReprocessamento.Status);
        Assert.Equal(command.Motivo, createdReprocessamento.Motivo);
        Assert.Equal(command.DataInicial, createdReprocessamento.DataInicial);
        Assert.Equal(command.DataFinal, createdReprocessamento.DataFinal);
        Assert.NotNull(createdIdem);
        Assert.Equal(202, createdIdem.ResponseStatusCode);
        Assert.Equal(result.ReprocessamentoId, createdIdem.LedgerEntryId);
        Assert.Contains(result.ReprocessamentoId.ToString(), createdIdem.ResponseBody);
        Assert.NotNull(createdOutbox);
        Assert.Equal("ReprocessamentoLancamentos", createdOutbox.AggregateType);
        Assert.Equal(result.ReprocessamentoId, createdOutbox.AggregateId);
        Assert.Equal(ReprocessamentoLancamentosSolicitadoV1.EventType, createdOutbox.EventType);
        Assert.Equal(Guid.Parse(command.CorrelationId), createdOutbox.CorrelationId);
        var outboxEvent = JsonSerializer.Deserialize<ReprocessamentoLancamentosSolicitadoV1>(
            createdOutbox.Payload,
            JsonOptions);
        Assert.NotNull(outboxEvent);
        Assert.Equal(result.ReprocessamentoId, outboxEvent.ReprocessamentoId);
        Assert.Equal("Pending", outboxEvent.Status);
        Assert.Equal(command.Motivo, outboxEvent.Motivo);
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
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(7)));
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(reprocessamentoRepo, idemRepo, outboxRepo, uow);

        var result = await sut.Handle(command, CancellationToken.None);
        Assert.Equal(expected, result);
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
        var command = ValidCommand() with
        {
            AuthorizedMerchantIds = ["m2"]
        };
        var sut = CreateSut(reprocessamentoRepo, idemRepo, outboxRepo, uow);

        async Task Act()
        {
            _ = await sut.Handle(command, CancellationToken.None);
        }

        await Assert.ThrowsAsync<ForbiddenException>(Act);
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
