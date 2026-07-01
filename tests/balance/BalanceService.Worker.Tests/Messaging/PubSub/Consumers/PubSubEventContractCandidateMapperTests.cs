using BalanceService.Worker.Messaging.PubSub.Consumers;
using BalanceService.Worker.Messaging.PubSub.Tracing;

namespace BalanceService.Worker.Tests.Messaging.PubSub.Consumers;

public sealed class PubSubEventContractCandidateMapperTests
{
    [Fact]
    public void Map_should_extract_event_contract_metadata_from_pubsub_attributes()
    {
        var attributes = new Dictionary<string, string>
        {
            [PubSubAttributeNames.EventType] = "LedgerEntryCreated.v2",
            [PubSubAttributeNames.EventId] = "evt-1"
        };

        var candidate = PubSubEventContractCandidateMapper.Map("{}", attributes);

        Assert.Equal("LedgerEntryCreated", candidate.EventName);
        Assert.Equal("v2", candidate.EventVersion);
        Assert.Equal("evt-1", candidate.Metadata?[PubSubAttributeNames.EventId]);
    }
}
