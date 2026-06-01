using System.Diagnostics;

using LedgerService.Application.Lancamentos.Events;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.UnitTests.Fixtures;
using Moq;

namespace LedgerService.UnitTests.Application.Lancamentos.Services;

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
        var clock = new TestClock(new DateTimeOffset(2026, 2, 16, 12, 0, 0, TimeSpan.Zero));

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

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object, clock);

        var result = await sut.ExecuteAsync(input, CancellationToken.None);
        Assert.Equal("CREDIT", result.Type);
        Assert.Equal(input.MerchantId, result.MerchantId);
        Assert.Equal("10.00", result.Amount);
        Assert.StartsWith("lan_", result.Id);
        Assert.Equal(input.Description, result.Description);
        Assert.Equal(input.ExternalReference, result.ExternalReference);
        Assert.False(string.IsNullOrWhiteSpace(result.OccurredAt));
        Assert.False(string.IsNullOrWhiteSpace(result.CreatedAt));
        Assert.NotNull(createdEntry);
        Assert.Equal(input.MerchantId, createdEntry!.MerchantId);
        Assert.Equal(LedgerEntryType.Credit, createdEntry!.Type);
        Assert.Equal(10.00m, createdEntry!.Amount);
        Assert.NotNull(createdIdem);
        Assert.Equal(input.MerchantId, createdIdem!.MerchantId);
        Assert.Equal(input.IdempotencyKey, createdIdem!.IdempotencyKey);
        Assert.Equal(201, createdIdem!.ResponseStatusCode);
        Assert.Equal(createdEntry!.Id, createdIdem!.LedgerEntryId);
        Assert.False(string.IsNullOrWhiteSpace(createdIdem!.ResponseBody));
        Assert.Equal(clock.UtcNow.DateTime, createdEntry.CreatedAt);
        Assert.Equal(clock.UtcNow.DateTime, createdIdem.CreatedAt);
        Assert.Equal(clock.UtcNow.DateTime.AddDays(7), createdIdem.ExpiresAt);
        Assert.NotNull(createdOutbox);
        Assert.Equal("LedgerEntry", createdOutbox!.AggregateType);
        Assert.Equal(createdEntry!.Id, createdOutbox!.AggregateId);
        Assert.Equal(LedgerEntryCreatedV1.EventType, createdOutbox!.EventType);
        Assert.Contains("\"merchantId\"", createdOutbox!.Payload);
        Assert.Equal(Guid.Parse(input.CorrelationId), createdOutbox!.CorrelationId);
        Assert.Equal(clock.UtcNow.DateTime, createdOutbox.OccurredAt);
        var outboxEvent = System.Text.Json.JsonSerializer.Deserialize<LedgerEntryCreatedV1>(createdOutbox.Payload, JsonOptions);
        Assert.NotNull(outboxEvent);
        Assert.Equal(result.Id, outboxEvent!.Id);
        Assert.Equal("CREDIT", outboxEvent.Type);
        Assert.Equal("10.00", outboxEvent.Amount);
        Assert.Equal(input.MerchantId, outboxEvent.MerchantId);
        Assert.Equal(input.Description, outboxEvent.Description);
        Assert.Equal(input.ExternalReference, outboxEvent.ExternalReference);
        Assert.Equal(input.CorrelationId, outboxEvent.CorrelationId);
        Assert.Equal(result.OccurredAt, outboxEvent.OccurredAt);
        Assert.Equal(result.CreatedAt, outboxEvent.CreatedAt);
        ledgerRepo.VerifyAll();
        idemRepo.VerifyAll();
        outboxRepo.VerifyAll();
        uow.VerifyAll();
        tx.VerifyAll();
    }

    [Fact]
    public async Task Should_persist_current_trace_context_in_outbox()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("LedgerService.UnitTests");
        using var activity = source.StartActivity("http.request", ActivityKind.Server);
        Assert.NotNull(activity);
        activity!.TraceStateString = "vendor=value";
        activity.AddBaggage("tenant", "poc");

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
        ledgerRepo.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        idemRepo.Setup(x => x.AddAsync(It.IsAny<IdempotencyRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        OutboxMessage? createdOutbox = null;
        outboxRepo.Setup(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((m, _) => createdOutbox = m)
            .Returns(Task.CompletedTask);

        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        await sut.ExecuteAsync(input, CancellationToken.None);
        Assert.NotNull(createdOutbox);
        Assert.Equal(activity.Id, createdOutbox!.TraceParent);
        Assert.Equal("vendor=value", createdOutbox.TraceState);
        Assert.Equal("tenant=poc", createdOutbox.Baggage);
    }

    [Fact]
    public async Task Should_create_debit_response_and_outbox_event()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var input = LancamentoFixture.ValidInput(type: "DEBIT", amount: "-15.50");

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);

        LedgerEntry? createdEntry = null;
        ledgerRepo.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((e, _) => createdEntry = e)
            .Returns(Task.CompletedTask);

        idemRepo.Setup(x => x.AddAsync(It.IsAny<IdempotencyRecord>(), It.IsAny<CancellationToken>()))
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
        Assert.Equal("DEBIT", result.Type);
        Assert.Equal("-15.50", result.Amount);
        Assert.NotNull(createdEntry);
        Assert.Equal(LedgerEntryType.Debit, createdEntry!.Type);
        Assert.Equal(-15.50m, createdEntry.Amount);
        Assert.NotNull(createdOutbox);
        var outboxEvent = System.Text.Json.JsonSerializer.Deserialize<LedgerEntryCreatedV1>(createdOutbox!.Payload, JsonOptions);
        Assert.NotNull(outboxEvent);
        Assert.Equal("DEBIT", outboxEvent!.Type);
        Assert.Equal("-15.50", outboxEvent.Amount);
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
            createdAt: DateTime.Now,
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var act = async () => await sut.ExecuteAsync(input, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(act);
        Assert.Contains("Idempotency-Key already used with a different payload", ex.Message);
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
            createdAt: DateTime.Now,
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(changedInput.MerchantId, changedInput.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var act = async () => await sut.ExecuteAsync(changedInput, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(act);
        Assert.Contains("Idempotency-Key already used with a different payload", ex.Message);
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
            createdAt: DateTime.Now,
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var result = await sut.ExecuteAsync(input, CancellationToken.None);
        Assert.Equal("lan_12345678", result.Id);
        Assert.Equal("CREDIT", result.Type);
        Assert.Equal("10.00", result.Amount);
    }

    [Fact]
    public async Task Should_throw_conflict_when_idempotency_record_has_unreplayable_response_body()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var existing = new IdempotencyRecord(
            merchantId: input.MerchantId,
            idempotencyKey: input.IdempotencyKey,
            requestHash: ComputeRequestHash(input),
            ledgerEntryId: Guid.NewGuid(),
            responseStatusCode: 201,
            responseBody: "null",
            createdAt: DateTime.Now,
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var act = async () => await sut.ExecuteAsync(input, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(act);
        Assert.Contains("Unable to replay idempotent response", ex.Message);
    }

    [Fact]
    public async Task Should_replay_response_when_idempotency_payload_differs_only_by_optional_text_whitespace()
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var originalInput = LancamentoFixture.ValidInput(
            type: "CREDIT",
            amount: "10.00") with
        {
            Description = "desc",
            ExternalReference = "ext"
        };
        var replayInput = originalInput with
        {
            Description = "  desc  ",
            ExternalReference = "  ext  "
        };

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var expectedReplay = new LancamentoDto(
            "lan_12345678",
            originalInput.MerchantId,
            "CREDIT",
            "10.00",
            "2026-02-16T00:00:00.0000000Z",
            "desc",
            "ext",
            "2026-02-16T00:00:00.0000000Z");

        var existing = new IdempotencyRecord(
            merchantId: originalInput.MerchantId,
            idempotencyKey: originalInput.IdempotencyKey,
            requestHash: ComputeRequestHash(originalInput),
            ledgerEntryId: Guid.NewGuid(),
            responseStatusCode: 201,
            responseBody: System.Text.Json.JsonSerializer.Serialize(expectedReplay, JsonOptions),
            createdAt: DateTime.Now,
            expiresAt: DateTime.Now.AddDays(7));

        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(replayInput.MerchantId, replayInput.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoService(ledgerRepo.Object, idemRepo.Object, outboxRepo.Object, uow.Object);

        var result = await sut.ExecuteAsync(replayInput, CancellationToken.None);
        Assert.Equal(expectedReplay, result);
        ledgerRepo.VerifyNoOtherCalls();
        outboxRepo.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static string ComputeRequestHash(CreateLancamentoInput input)
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
