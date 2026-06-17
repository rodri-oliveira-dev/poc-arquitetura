using System.Text.Json;

using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.UnitTests.Fixtures;
using Moq;

namespace LedgerService.UnitTests.Application.Lancamentos.Services;

public sealed class CreateLancamentoIdempotencyServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GenerateRequestHash_should_preserve_current_optional_text_normalization()
    {
        var repository = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var sut = new CreateLancamentoIdempotencyService(repository.Object);
        var input = LancamentoFixture.ValidInput(type: "credit", amount: "10.00") with
        {
            Description = "  desc  ",
            ExternalReference = "  ext  "
        };
        var normalizedInput = input with
        {
            Type = "CREDIT",
            Description = "desc",
            ExternalReference = "ext"
        };

        Assert.Equal(
            CreateLancamentoIdempotencyService.GenerateRequestHash(normalizedInput),
            CreateLancamentoIdempotencyService.GenerateRequestHash(input));
    }

    [Fact]
    public async Task TryReplayAsync_should_return_persisted_response_for_same_hash()
    {
        var repository = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");
        var expected = new LancamentoDto(
            "lan_12345678",
            Guid.NewGuid(),
            input.MerchantId,
            "CREDIT",
            "10.00",
            "2026-02-16T00:00:00.0000000Z",
            null,
            null,
            "2026-02-16T00:00:00.0000000Z");
        var sut = new CreateLancamentoIdempotencyService(repository.Object);
        var requestHash = CreateLancamentoIdempotencyService.GenerateRequestHash(input);
        var existing = new IdempotencyRecord(
            input.MerchantId,
            input.IdempotencyKey,
            requestHash,
            Guid.NewGuid(),
            201,
            JsonSerializer.Serialize(expected, JsonOptions),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7));

        repository.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var replay = await sut.TryReplayAsync(input, requestHash, CancellationToken.None);

        Assert.Equal(expected, replay);
    }

    [Fact]
    public async Task TryReplayAsync_should_throw_conflict_for_different_hash()
    {
        var repository = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var input = LancamentoFixture.ValidInput(type: "CREDIT", amount: "10.00");
        var existing = new IdempotencyRecord(
            input.MerchantId,
            input.IdempotencyKey,
            "different-hash",
            Guid.NewGuid(),
            201,
            "{}",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7));

        repository.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = new CreateLancamentoIdempotencyService(repository.Object);
        var act = async () => await sut.TryReplayAsync(
            input,
            CreateLancamentoIdempotencyService.GenerateRequestHash(input),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(act);
        Assert.Contains("Idempotency-Key already used with a different payload", exception.Message);
    }
}
