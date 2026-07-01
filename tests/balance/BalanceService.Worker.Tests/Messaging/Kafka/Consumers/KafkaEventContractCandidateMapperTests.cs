using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Kafka.Consumers;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Consumers;

public sealed class KafkaEventContractCandidateMapperTests
{
    [Fact]
    public void Map_should_extract_event_contract_metadata_from_kafka_headers()
    {
        var headers = new Dictionary<string, string>
        {
            [MessageAttributeNames.EventType] = "LedgerEntryCreated.v2",
            [MessageAttributeNames.EventId] = "evt-1"
        };

        var candidate = KafkaEventContractCandidateMapper.Map("{}", headers);

        Assert.Equal("LedgerEntryCreated", candidate.EventName);
        Assert.Equal("v2", candidate.EventVersion);
        Assert.Equal("evt-1", candidate.Metadata?[MessageAttributeNames.EventId]);
    }
}
