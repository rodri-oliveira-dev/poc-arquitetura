using System.Collections;

namespace IdentityService.Application.Idempotency;

public static class IdempotencyFailureMetadata
{
    private const string FailureStageKey = "IdentityService.Idempotency.FailureStage";

    public static void SetFailureStage(Exception exception, string failureStage)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (string.IsNullOrWhiteSpace(failureStage))
            throw new ArgumentException("Failure stage is required.", nameof(failureStage));

        exception.Data[FailureStageKey] = failureStage;
    }

    public static string? GetFailureStage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Data is IDictionary data && data.Contains(FailureStageKey)
            ? data[FailureStageKey] as string
            : null;
    }
}
