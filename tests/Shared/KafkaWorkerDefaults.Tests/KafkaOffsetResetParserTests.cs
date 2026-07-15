using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace KafkaWorkerDefaults.Tests;

public sealed class KafkaOffsetResetParserTests
{
    [Theory]
    [InlineData("Earliest", AutoOffsetReset.Earliest)]
    [InlineData(" earliest ", AutoOffsetReset.Earliest)]
    [InlineData("Latest", AutoOffsetReset.Latest)]
    [InlineData(" latest ", AutoOffsetReset.Latest)]
    public void Parse_should_accept_supported_values_ignoring_case_and_outer_whitespace(
        string value,
        AutoOffsetReset expected)
    {
        AutoOffsetReset result = KafkaOffsetResetParser.Parse(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unexpected")]
    public void Parse_should_fallback_to_earliest_when_value_is_blank_or_unknown(string value)
    {
        AutoOffsetReset result = KafkaOffsetResetParser.Parse(value);

        Assert.Equal(AutoOffsetReset.Earliest, result);
    }

    [Fact]
    public void Parse_should_reject_null_value()
    {
        Assert.Throws<ArgumentNullException>(() => KafkaOffsetResetParser.Parse(null!));
    }
}
