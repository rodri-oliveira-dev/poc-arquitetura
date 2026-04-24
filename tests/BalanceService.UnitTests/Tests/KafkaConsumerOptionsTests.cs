using BalanceService.Infrastructure.Messaging.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace BalanceService.UnitTests.Tests;

public sealed class KafkaConsumerOptionsTests
{
    [Fact]
    public void KafkaConsumerOptions_should_preserve_current_retry_delay_defaults()
    {
        var options = new KafkaConsumerOptions();

        options.InvalidMessageRetryDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.ConsumeErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.ProcessingErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void KafkaConsumerOptions_should_bind_retry_delays_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:Consumer:InvalidMessageRetryDelay"] = "00:00:03",
                ["Kafka:Consumer:ConsumeErrorRetryDelay"] = "00:00:04",
                ["Kafka:Consumer:ProcessingErrorRetryDelay"] = "00:00:06"
            })
            .Build();

        var options = configuration
            .GetSection(KafkaConsumerOptions.SectionName)
            .Get<KafkaConsumerOptions>();

        options.Should().NotBeNull();
        options!.InvalidMessageRetryDelay.Should().Be(TimeSpan.FromSeconds(3));
        options.ConsumeErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(4));
        options.ProcessingErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(6));
    }
}
