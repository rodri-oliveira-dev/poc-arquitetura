namespace AuditService.Worker.Messaging.Kafka;

internal sealed record AuditRecordRequestedProcessingResult(bool ShouldCommit, string Result)
{
    public static AuditRecordRequestedProcessingResult Success { get; } = new(true, "success");
    public static AuditRecordRequestedProcessingResult Duplicate { get; } = new(true, "duplicate");
    public static AuditRecordRequestedProcessingResult DeadLetter { get; } = new(true, "dlq");
    public static AuditRecordRequestedProcessingResult NotProcessed { get; } = new(false, "not_processed");
}
