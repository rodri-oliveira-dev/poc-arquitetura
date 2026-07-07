using BalanceService.Application.Contracts.Events;

namespace BalanceService.UnitTests.Application.Contracts.Events;

public sealed class JsonSchemaEventContractValidatorTests
{
    private readonly JsonSchemaEventContractValidator _sut = new(new EmbeddedEventContractSchemaCatalog());

    [Fact]
    public void Validate_should_accept_valid_payload()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate(ReadExample("ledger-entry-created.v2.valid.json")));

        Assert.True(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.None, result.ErrorCode);
        Assert.Equal("LedgerEntryCreated", result.EventName);
        Assert.Equal("v2", result.EventVersion);
    }

    [Fact]
    public void Validate_should_reject_invalid_payload()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate(ReadExample("ledger-entry-created.v2.invalid.json")));

        Assert.False(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.InvalidPayload, result.ErrorCode);
        Assert.Equal("LedgerEntryCreated", result.EventName);
        Assert.Equal("v2", result.EventVersion);
        Assert.Contains("currency", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_should_reject_unknown_version()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate(ReadExample("ledger-entry-created.v2.valid.json"), eventVersion: "v99"));

        Assert.False(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.UnsupportedVersion, result.ErrorCode);
        Assert.Equal("LedgerEntryCreated", result.EventName);
        Assert.Equal("v99", result.EventVersion);
    }

    [Fact]
    public void Validate_should_reject_missing_event_name()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate(ReadExample("ledger-entry-created.v2.valid.json"), eventName: null));

        Assert.False(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.EventNameMissing, result.ErrorCode);
    }

    [Fact]
    public void Validate_should_reject_missing_event_version()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate(ReadExample("ledger-entry-created.v2.valid.json"), eventVersion: null));

        Assert.False(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.EventVersionMissing, result.ErrorCode);
        Assert.Equal("LedgerEntryCreated", result.EventName);
    }

    [Fact]
    public void Validate_should_reject_malformed_json()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate("{"));

        Assert.False(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.InvalidJson, result.ErrorCode);
        Assert.Equal("LedgerEntryCreated", result.EventName);
        Assert.Equal("v2", result.EventVersion);
    }

    [Fact]
    public void Validate_should_report_schema_not_found_for_unknown_event_name()
    {
        EventContractValidationResult result = _sut.Validate(CreateCandidate("{}", eventName: "UnknownEvent"));

        Assert.False(result.IsValid);
        Assert.Equal(EventContractValidationErrorCode.SchemaNotFound, result.ErrorCode);
        Assert.Equal("UnknownEvent", result.EventName);
        Assert.Equal("v2", result.EventVersion);
    }

    private static EventContractValidationCandidate CreateCandidate(
        string payload,
        string? eventName = "LedgerEntryCreated",
        string? eventVersion = "v2")
        => new(eventName, eventVersion, payload);

    private static string ReadExample(string fileName)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), "contracts", "events", "examples", fileName));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
