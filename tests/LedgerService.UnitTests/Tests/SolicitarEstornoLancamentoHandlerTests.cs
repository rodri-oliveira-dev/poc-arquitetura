using System.Text.Json;

using FluentAssertions;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using Moq;

namespace LedgerService.UnitTests.Tests;

public sealed class SolicitarEstornoLancamentoHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_create_pending_estorno_and_outbox_message()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var estornoRepo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var lancamento = ValidLedgerEntry();
        var command = ValidCommand(lancamento.Id);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        ledgerRepo.Setup(x => x.GetByIdAsync(command.LancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lancamento);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(lancamento.MerchantId, command.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);
        estornoRepo.Setup(x => x.GetActiveByLancamentoOriginalIdAsync(command.LancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstornoLancamento?)null);

        EstornoLancamento? createdEstorno = null;
        estornoRepo.Setup(x => x.AddAsync(It.IsAny<EstornoLancamento>(), It.IsAny<CancellationToken>()))
            .Callback<EstornoLancamento, CancellationToken>((e, _) => createdEstorno = e)
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

        var sut = CreateSut(ledgerRepo, estornoRepo, idemRepo, outboxRepo, uow);

        var result = await sut.Handle(command, CancellationToken.None);

        result.EstornoId.Should().NotBeEmpty();
        result.LancamentoOriginalId.Should().Be(lancamento.Id);
        result.Status.Should().Be(EstornoLancamentoStatus.Pending.ToString());
        result.StatusUrl.Should().Be($"/api/v1/lancamentos/estornos/{result.EstornoId}");
        result.MerchantId.Should().Be(lancamento.MerchantId);

        createdEstorno.Should().NotBeNull();
        createdEstorno!.LancamentoOriginalId.Should().Be(lancamento.Id);
        createdEstorno.MerchantId.Should().Be(lancamento.MerchantId);
        createdEstorno.Status.Should().Be(EstornoLancamentoStatus.Pending);

        createdIdem.Should().NotBeNull();
        createdIdem!.ResponseStatusCode.Should().Be(202);
        createdIdem.LedgerEntryId.Should().Be(lancamento.Id);
        createdIdem.ResponseBody.Should().Contain(result.EstornoId.ToString());

        createdOutbox.Should().NotBeNull();
        createdOutbox!.AggregateType.Should().Be("LancamentoEstorno");
        createdOutbox.AggregateId.Should().Be(result.EstornoId);
        createdOutbox.EventType.Should().Be(LancamentoEstornoSolicitadoV1.EventType);
        createdOutbox.CorrelationId.Should().Be(Guid.Parse(command.CorrelationId));

        var outboxEvent = JsonSerializer.Deserialize<LancamentoEstornoSolicitadoV1>(createdOutbox.Payload, JsonOptions);
        outboxEvent.Should().NotBeNull();
        outboxEvent!.EstornoId.Should().Be(result.EstornoId);
        outboxEvent.LancamentoOriginalId.Should().Be(lancamento.Id);
        outboxEvent.Status.Should().Be("Pending");
        outboxEvent.Motivo.Should().Be(command.Motivo);

        ledgerRepo.VerifyAll();
        estornoRepo.VerifyAll();
        idemRepo.VerifyAll();
        outboxRepo.VerifyAll();
        uow.VerifyAll();
        tx.VerifyAll();
    }

    [Fact]
    public async Task Should_throw_not_found_when_lancamento_does_not_exist()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var estornoRepo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);
        var command = ValidCommand(Guid.NewGuid());

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        ledgerRepo.Setup(x => x.GetByIdAsync(command.LancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerEntry?)null);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(ledgerRepo, estornoRepo, idemRepo, outboxRepo, uow);

        var act = async () => await sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_throw_conflict_when_lancamento_has_active_estorno()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var estornoRepo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var lancamento = ValidLedgerEntry();
        var command = ValidCommand(lancamento.Id);
        var active = new EstornoLancamento(lancamento.Id, lancamento.MerchantId, "Erro operacional original", Guid.NewGuid());

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        ledgerRepo.Setup(x => x.GetByIdAsync(command.LancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lancamento);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(lancamento.MerchantId, command.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);
        estornoRepo.Setup(x => x.GetActiveByLancamentoOriginalIdAsync(command.LancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(ledgerRepo, estornoRepo, idemRepo, outboxRepo, uow);

        var act = async () => await sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*solicitacao ativa*");
    }

    [Fact]
    public async Task Should_replay_idempotent_response()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var estornoRepo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var lancamento = ValidLedgerEntry();
        var command = ValidCommand(lancamento.Id);
        var estornoId = Guid.NewGuid();
        var expected = new SolicitarEstornoLancamentoResult(
            estornoId,
            lancamento.Id,
            "Pending",
            $"/api/v1/lancamentos/estornos/{estornoId}",
            lancamento.MerchantId);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        ledgerRepo.Setup(x => x.GetByIdAsync(command.LancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lancamento);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(lancamento.MerchantId, command.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecord(
                lancamento.MerchantId,
                command.IdempotencyKey,
                ComputeRequestHash(command),
                lancamento.Id,
                202,
                JsonSerializer.Serialize(expected, JsonOptions),
                DateTime.Now.AddDays(7)));
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(ledgerRepo, estornoRepo, idemRepo, outboxRepo, uow);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().Be(expected);
        estornoRepo.VerifyNoOtherCalls();
        outboxRepo.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SolicitarEstornoLancamentoHandler CreateSut(
        Mock<ILedgerEntryRepository> ledgerRepo,
        Mock<IEstornoLancamentoRepository> estornoRepo,
        Mock<IIdempotencyRecordRepository> idemRepo,
        Mock<IOutboxMessageRepository> outboxRepo,
        Mock<IUnitOfWork> uow)
        => new(ledgerRepo.Object, estornoRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

    private static LedgerEntry ValidLedgerEntry()
        => new(
            "m1",
            LedgerEntryType.Credit,
            10m,
            DateTime.Now,
            "desc",
            "ext",
            Guid.NewGuid());

    private static SolicitarEstornoLancamentoCommand ValidCommand(Guid lancamentoId)
        => new(
            lancamentoId,
            "Erro operacional no lancamento original",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            ["m1"]);

    private static string ComputeRequestHash(SolicitarEstornoLancamentoCommand command)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            command.LancamentoId,
            Motivo = command.Motivo.Trim()
        }, JsonOptions);

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
