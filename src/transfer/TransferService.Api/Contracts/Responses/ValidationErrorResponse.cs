namespace TransferService.Api.Contracts.Responses;

public sealed class ValidationErrorResponse
{
    public string Type { get; init; } = "https://httpstatuses.com/400";

    public string Title { get; init; } = "Invalid request";

    public int Status { get; init; } = 400;

    public string Detail { get; init; } = "One or more validation errors occurred.";

    public Dictionary<string, string[]> Errors { get; init; } = new();

    public string? CorrelationId
    {
        get; init;
    }
}
