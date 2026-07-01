using System.ComponentModel.DataAnnotations;

using LedgerService.Worker.Messaging.PubSub.Configuration;

namespace LedgerService.Worker.Tests.Messaging.PubSub.Configuration;

public sealed class PubSubProducerOptionsTests
{
    [Theory]
    [InlineData(nameof(PubSubProducerOptions.ProjectId))]
    [InlineData(nameof(PubSubProducerOptions.DefaultTopicId))]
    public void PubSubProducerOptions_should_require_mandatory_values(string missingProperty)
    {
        var options = ValidOptions(missingProperty);

        var validationResults = Validate(options);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(missingProperty));
    }

    [Fact]
    public void PubSubProducerOptions_should_accept_valid_configuration()
    {
        var validationResults = Validate(ValidOptions());

        Assert.Empty(validationResults);
    }

    private static PubSubProducerOptions ValidOptions(string? missingProperty = null)
    {
        return new PubSubProducerOptions
        {
            ProjectId = missingProperty == nameof(PubSubProducerOptions.ProjectId) ? string.Empty : "poc-project",
            DefaultTopicId = missingProperty == nameof(PubSubProducerOptions.DefaultTopicId) ? string.Empty : "ledger-events"
        };
    }

    private static List<ValidationResult> Validate(PubSubProducerOptions options)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), validationResults, validateAllProperties: true);
        return validationResults;
    }
}
