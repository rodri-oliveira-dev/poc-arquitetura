using System.ComponentModel.DataAnnotations;

namespace BalanceService.Worker.Messaging.PubSub.Configuration;

public sealed class PubSubConsumerOptions : IValidatableObject
{
    public const string SectionName = "PubSub:Consumer";

    [Required]
    public string ProjectId { get; init; } = string.Empty;

    [Required]
    public string SubscriptionId { get; init; } = string.Empty;

    [Required]
    public string DeadLetterTopicId { get; init; } = string.Empty;

    public bool EnableExactlyOnceDelivery
    {
        get; init;
    }

    public TimeSpan ProcessingErrorRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    [Range(1, int.MaxValue)]
    public int SubscriberClientCount { get; init; } = 1;

    [Range(1, 600)]
    public int AckDeadlineSeconds { get; init; } = 60;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProcessingErrorRetryDelay <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "PubSub Consumer ProcessingErrorRetryDelay deve ser maior que zero.",
                [nameof(ProcessingErrorRetryDelay)]);
        }
    }
}
