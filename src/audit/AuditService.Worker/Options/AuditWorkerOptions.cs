namespace AuditService.Worker.Options;

public sealed class AuditWorkerOptions
{
    public const string SectionName = "AuditService:Worker";

    public bool Enabled
    {
        get; init;
    }

    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromMinutes(5);
}
