using FluentAssertions;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.UnitTests.Fixtures;
using Moq;

namespace LedgerService.UnitTests.Tests;

public sealed class CreateLancamentoServiceTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_create_ledger_entry_and_outbox_and_idempotency_record()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);

        LedgerEntry? createdEntry = null;
        ledgerRepo.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((e, _) => createdEntry = e)
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

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var result = await sut.ExecuteAsync(input, CancellationToken.None);

        result.Type.Should().Be("CREDIT");
        result.MerchantId.Should().Be(input.MerchantId);
        result.Amount.Should().Be("10.00");
        result.Id.Should().StartWith("lan_");

        createdEntry.Should().NotBeNull();
        createdEntry!.MerchantId.Should().Be(input.MerchantId);
        createdEntry!.Type.Should().Be(LedgerEntryType.Credit);
        createdEntry!.Amount.Should().Be(10.00m);

        createdIdem.Should().NotBeNull();
        createdIdem!.MerchantId.Should().Be(input.MerchantId);
        createdIdem!.IdempotencyKey.Should().Be(input.IdempotencyKey);
        createdIdem!.ResponseStatusCode.Should().Be(201);
        createdIdem!.LedgerEntryId.Should().Be(createdEntry!.Id);
        createdIdem!.ResponseBody.Should().NotBeNullOrWhiteSpace();

        createdOutbox.Should().NotBeNull();
        createdOutbox!.AggregateType.Should().Be("LedgerEntry");
        createdOutbox!.AggregateId.Should().Be(createdEntry!.Id);
        createdOutbox!.EventType.Should().Be("LedgerEntryCreated");
        createdOutbox!.Payload.Should().Contain("\"merchantId\"");
        createdOutbox!.CorrelationId.Should().Be(Guid.Parse(input.CorrelationId));

        ledgerRepo.VerifyAll();
        idemRepo.VerifyAll();
        outboxRepo.VerifyAll();
        uow.VerifyAll();
        tx.VerifyAll();
    }

    [Fact]
    public async Task Should_throw_conflict_when_idempotency_key_used_with_different_payload()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        // requestHash será calculado pelo service; forçamos um hash diferente aqui.
        var existing = new IdempotencyRecord(
            merchantId: input.MerchantId,
            idempotencyKey: input.IdempotencyKey,
            requestHash: "different-hash",
            ledgerEntryId: Guid.NewGuid(),
            responseStatusCode: 201,
            responseBody: "{}",
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var act = async () => await sut.ExecuteAsync(input, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Idempotency-Key already used with a different payload*");
    }

    [Theory]
    [InlineData("desc changed", "ext")]
    [InlineData("desc", "ext changed")]
    public async Task Should_throw_conflict_when_idempotency_key_reused_after_description_or_external_reference_changes(
        string description,
        string externalReference)
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var originalInput = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");
        var changedInput = originalInput with
        {
            Description = description,
            ExternalReference = externalReference
        };

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var existing = new IdempotencyRecord(
            merchantId: originalInput.MerchantId,
            idempotencyKey: originalInput.IdempotencyKey,
            requestHash: ComputeRequestHash(originalInput),
            ledgerEntryId: Guid.NewGuid(),
            responseStatusCode: 201,
            responseBody: "{}",
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(changedInput.MerchantId, changedInput.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var act = async () => await sut.ExecuteAsync(changedInput, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Idempotency-Key already used with a different payload*");
    }

    [Fact]
    public async Task Should_replay_response_when_idempotency_record_has_response_body()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var expectedHash = ComputeRequestHash(input);

        var replayJson = "{\"id\":\"lan_12345678\",\"merchantId\":\"m1\",\"type\":\"CREDIT\",\"amount\":\"10.00\",\"occurredAt\":\"2026-02-16T00:00:00.0000000Z\",\"description\":null,\"externalReference\":null,\"createdAt\":\"2026-02-16T00:00:00.0000000Z\"}";

        var existing = new IdempotencyRecord(
            merchantId: input.MerchantId,
            idempotencyKey: input.IdempotencyKey,
            requestHash: expectedHash,
            ledgerEntryId: Guid.NewGuid(),
            responseStatusCode: 201,
            responseBody: replayJson,
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var result = await sut.ExecuteAsync(input, CancellationToken.None);

        result.Id.Should().Be("lan_12345678");
        result.Type.Should().Be("CREDIT");
        result.Amount.Should().Be("10.00");
    }

    private static string ComputeRequestHash(LedgerService.Application.Lancamentos.Inputs.CreateLancamento.CreateLancamentoInput input)
    {
        var canonical = System.Text.Json.JsonSerializer.Serialize(new
        {
            input.MerchantId,
            Type = input.Type.ToUpperInvariant(),
            input.Amount,
            Description = NormalizeOptionalText(input.Description),
            ExternalReference = NormalizeOptionalText(input.ExternalReference)
        }, JsonOptions);

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
