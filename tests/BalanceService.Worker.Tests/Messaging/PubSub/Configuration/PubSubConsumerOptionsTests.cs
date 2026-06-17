using System.ComponentModel.DataAnnotations;

using BalanceService.Worker.Messaging.PubSub.Configuration;

namespace BalanceService.Worker.Tests.Messaging.PubSub.Configuration;

public sealed class PubSubConsumerOptionsTests
{
    [Theory]
    [InlineData(nameof(PubSubConsumerOptions.ProjectId))]
    [InlineData(nameof(PubSubConsumerOptions.SubscriptionId))]
    [InlineData(nameof(PubSubConsumerOptions.DeadLetterTopicId))]
    public void PubSubConsumerOptions_should_require_mandatory_values(string missingProperty)
    {
        var options = ValidOptions(missingProperty);

        var validationResults = Validate(options);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(missingProperty));
    }

    [Fact]
    public void PubSubConsumerOptions_should_reject_non_positive_processing_retry_delay()
    {
        var options = ValidOptions(processingErrorRetryDelay: TimeSpan.Zero);

        var validationResults = Validate(options);

        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(nameof(PubSubConsumerOptions.ProcessingErrorRetryDelay)));
    }

    [Fact]
    public void PubSubConsumerOptions_should_accept_valid_configuration()
    {
        var validationResults = Validate(ValidOptions());

        Assert.Empty(validationResults);
    }

    private static PubSubConsumerOptions ValidOptions(
        string? missingProperty = null,
        TimeSpan? processingErrorRetryDelay = null)
    {
        return new PubSubConsumerOptions
        {
            ProjectId = missingProperty == nameof(PubSubConsumerOptions.ProjectId) ? string.Empty : "poc-project",
            SubscriptionId = missingProperty == nameof(PubSubConsumerOptions.SubscriptionId) ? string.Empty : "ledger-events-balance",
            DeadLetterTopicId = missingProperty == nameof(PubSubConsumerOptions.DeadLetterTopicId) ? string.Empty : "ledger-events-dlq",
            ProcessingErrorRetryDelay = processingErrorRetryDelay ?? TimeSpan.FromSeconds(5)
        };
    }

    private static List<ValidationResult> Validate(PubSubConsumerOptions options)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), validationResults, validateAllProperties: true);
        return validationResults;
    }
}
