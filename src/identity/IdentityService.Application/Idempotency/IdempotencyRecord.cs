namespace IdentityService.Application.Idempotency;

public sealed class IdempotencyRecord
{
    public const int OperationNameMaxLength = 64;
    public const int IdempotencyKeyMaxLength = 128;
    public const int RequestHashMaxLength = 64;
    public const int StatusMaxLength = 32;
    public const int FailureStageMaxLength = 64;
    public const int ErrorMessageMaxLength = 2_000;

    private IdempotencyRecord()
    {
        Id = Guid.Empty;
        OperationName = string.Empty;
        IdempotencyKey = string.Empty;
        RequestHash = string.Empty;
        Status = IdempotencyStatus.Processing;
    }

    private IdempotencyRecord(
        Guid id,
        string operationName,
        string idempotencyKey,
        string requestHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? lockedUntilUtc)
    {
        Id = id;
        OperationName = operationName;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        Status = IdempotencyStatus.Processing;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        ExpiresAtUtc = EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        LockedUntilUtc = EnsureNullableUtc(lockedUntilUtc, nameof(lockedUntilUtc));
    }

    public Guid Id
    {
        get; private set;
    }

    public string OperationName
    {
        get; private set;
    }

    public string IdempotencyKey
    {
        get; private set;
    }

    public string RequestHash
    {
        get; private set;
    }

    public IdempotencyStatus Status
    {
        get; private set;
    }

    public int? ResponseStatusCode
    {
        get; private set;
    }

    public string? ResponseBody
    {
        get; private set;
    }

    public Guid? ResourceId
    {
        get; private set;
    }

    public DateTime CreatedAtUtc
    {
        get; private set;
    }

    public DateTime? CompletedAtUtc
    {
        get; private set;
    }

    public DateTime ExpiresAtUtc
    {
        get; private set;
    }

    public DateTime? LockedUntilUtc
    {
        get; private set;
    }

    public string? FailureStage
    {
        get; private set;
    }

    public string? ErrorMessage
    {
        get; private set;
    }

    public static IdempotencyRecord StartProcessing(
        string operationName,
        string idempotencyKey,
        string requestHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? lockedUntilUtc = null)
    {
        ValidateRequired(operationName, nameof(operationName), OperationNameMaxLength);
        ValidateRequired(idempotencyKey, nameof(idempotencyKey), IdempotencyKeyMaxLength);
        ValidateRequired(requestHash, nameof(requestHash), RequestHashMaxLength);

        return new IdempotencyRecord(
            Guid.NewGuid(),
            operationName,
            idempotencyKey,
            requestHash,
            createdAtUtc,
            expiresAtUtc,
            lockedUntilUtc);
    }

    public void MarkCompleted(
        int responseStatusCode,
        string responseBody,
        Guid? resourceId,
        DateTime completedAtUtc)
    {
        EnsureProcessing();

        if (responseStatusCode is < 100 or > 599)
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode), "Response status code must be valid HTTP status.");

        if (string.IsNullOrWhiteSpace(responseBody))
            throw new ArgumentException("Response body is required.", nameof(responseBody));

        Status = IdempotencyStatus.Completed;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        ResourceId = resourceId;
        CompletedAtUtc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        LockedUntilUtc = null;
        FailureStage = null;
        ErrorMessage = null;
    }

    public void MarkFailed(string failureStage, string? errorMessage, DateTime completedAtUtc)
    {
        EnsureCanFail();
        ValidateRequired(failureStage, nameof(failureStage), FailureStageMaxLength);

        Status = IdempotencyStatus.Failed;
        ResponseStatusCode = null;
        ResponseBody = null;
        ResourceId = null;
        CompletedAtUtc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        LockedUntilUtc = null;
        FailureStage = failureStage;
        ErrorMessage = Truncate(errorMessage, ErrorMessageMaxLength);
    }

    public void RestartProcessing(DateTime nowUtc, DateTime? lockedUntilUtc = null)
    {
        if (Status != IdempotencyStatus.Failed)
            throw new InvalidOperationException("Only failed idempotency records can be restarted.");

        Status = IdempotencyStatus.Processing;
        CompletedAtUtc = null;
        LockedUntilUtc = EnsureNullableUtc(lockedUntilUtc, nameof(lockedUntilUtc));
        FailureStage = null;
        ErrorMessage = null;
        _ = EnsureUtc(nowUtc, nameof(nowUtc));
    }

    private void EnsureProcessing()
    {
        if (Status != IdempotencyStatus.Processing)
            throw new InvalidOperationException($"Idempotency record cannot transition from '{Status}'.");
    }

    private void EnsureCanFail()
    {
        if (Status is not (IdempotencyStatus.Processing or IdempotencyStatus.Completed))
            throw new InvalidOperationException($"Idempotency record cannot transition from '{Status}'.");
    }

    private static void ValidateRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);

        if (value.Length > maxLength)
            throw new ArgumentException($"{parameterName} must have at most {maxLength} characters.", parameterName);
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName)
    {
        return value.Kind != DateTimeKind.Utc ? throw new ArgumentException($"{parameterName} must be UTC.", parameterName) : value;
    }

    private static DateTime? EnsureNullableUtc(DateTime? value, string parameterName)
        => value.HasValue ? EnsureUtc(value.Value, parameterName) : null;

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength
            ? value
            : value[..maxLength];
}
