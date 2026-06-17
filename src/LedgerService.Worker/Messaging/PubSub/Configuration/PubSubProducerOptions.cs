using System.ComponentModel.DataAnnotations;

namespace LedgerService.Worker.Messaging.PubSub.Configuration;

public sealed class PubSubProducerOptions
{
    public const string SectionName = "PubSub:Producer";

    [Required]
    public string ProjectId { get; init; } = string.Empty;

    [Required]
    public string DefaultTopicId { get; init; } = string.Empty;

    public bool EnableMessageOrdering
    {
        get; init;
    }

    public Dictionary<string, string> TopicMap { get; init; } = new();

    [Range(1, int.MaxValue)]
    public int ShutdownTimeoutSeconds { get; init; } = 30;
}
