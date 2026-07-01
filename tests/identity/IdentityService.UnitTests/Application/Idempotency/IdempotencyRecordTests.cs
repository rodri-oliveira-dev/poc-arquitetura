using IdentityService.Application.Idempotency;

namespace IdentityService.UnitTests.Application.Idempotency;

public sealed class IdempotencyRecordTests
{
    private const string RequestHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void StartProcessing_should_create_processing_record_with_utc_dates()
    {
        var createdAtUtc = new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = new DateTime(2026, 06, 27, 12, 0, 0, DateTimeKind.Utc);

        var record = IdempotencyRecord.StartProcessing(
            "CreateUser",
            "idem-1",
            RequestHash,
            createdAtUtc,
            expiresAtUtc);

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal("CreateUser", record.OperationName);
        Assert.Equal("idem-1", record.IdempotencyKey);
        Assert.Equal(RequestHash, record.RequestHash);
        Assert.Equal(IdempotencyStatus.Processing, record.Status);
        Assert.Equal(createdAtUtc, record.CreatedAtUtc);
        Assert.Equal(expiresAtUtc, record.ExpiresAtUtc);
    }

    [Fact]
    public void StartProcessing_should_reject_non_utc_dates()
    {
        var localDate = new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => IdempotencyRecord.StartProcessing(
            "CreateUser",
            "idem-1",
            RequestHash,
            localDate,
            new DateTime(2026, 06, 27, 12, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void MarkCompleted_should_store_final_response_and_clear_lock()
    {
        var record = CreateRecord();
        var completedAtUtc = new DateTime(2026, 06, 26, 12, 5, 0, DateTimeKind.Utc);
        var resourceId = Guid.NewGuid();

        record.MarkCompleted(201, /*lang=json,strict*/ """{"id":"user-1"}""", resourceId, completedAtUtc);

        Assert.Equal(IdempotencyStatus.Completed, record.Status);
        Assert.Equal(201, record.ResponseStatusCode);
        Assert.Equal(/*lang=json,strict*/ """{"id":"user-1"}""", record.ResponseBody);
        Assert.Equal(resourceId, record.ResourceId);
        Assert.Equal(completedAtUtc, record.CompletedAtUtc);
        Assert.Null(record.LockedUntilUtc);
        Assert.Null(record.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_should_store_error_and_clear_lock()
    {
        var record = CreateRecord();
        var completedAtUtc = new DateTime(2026, 06, 26, 12, 5, 0, DateTimeKind.Utc);

        record.MarkFailed(
            IdempotencyFailureStage.AfterIdentityProviderCompensated,
            "failed after compensation",
            completedAtUtc);

        Assert.Equal(IdempotencyStatus.Failed, record.Status);
        Assert.Equal(completedAtUtc, record.CompletedAtUtc);
        Assert.Null(record.LockedUntilUtc);
        Assert.Equal(IdempotencyFailureStage.AfterIdentityProviderCompensated, record.FailureStage);
        Assert.Equal("failed after compensation", record.ErrorMessage);
    }

    private static IdempotencyRecord CreateRecord()
        => IdempotencyRecord.StartProcessing(
            "CreateUser",
            "idem-1",
            RequestHash,
            new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 27, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 26, 12, 1, 0, DateTimeKind.Utc));
}
