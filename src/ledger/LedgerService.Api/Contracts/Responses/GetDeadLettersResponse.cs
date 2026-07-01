using System.Text.Json.Serialization;

namespace LedgerService.Api.Contracts.Responses;

public sealed class GetDeadLettersResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<DeadLetterOutboxMessageResponse> Items { get; init; } = Array.Empty<DeadLetterOutboxMessageResponse>();

    [JsonPropertyName("page")]
    public int Page
    {
        get; init;
    }

    [JsonPropertyName("pageSize")]
    public int PageSize
    {
        get; init;
    }

    [JsonPropertyName("totalCount")]
    public int TotalCount
    {
        get; init;
    }
}
