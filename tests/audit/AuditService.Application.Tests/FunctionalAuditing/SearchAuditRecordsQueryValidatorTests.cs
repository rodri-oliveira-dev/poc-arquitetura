using System.Globalization;

using AuditService.Application.FunctionalAuditing.SearchAuditRecords;
using AuditService.Domain.FunctionalAuditing;

namespace AuditService.Application.Tests.FunctionalAuditing;

public sealed class SearchAuditRecordsQueryValidatorTests
{
    private readonly SearchAuditRecordsQueryValidator _validator = new();

    [Fact]
    public void Validate_should_reject_query_without_period()
    {
        var query = ValidQuery() with
        {
            From = null,
            To = null
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SearchAuditRecordsQuery.From));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SearchAuditRecordsQuery.To));
    }

    [Fact]
    public void Validate_should_reject_page_size_above_limit()
    {
        var query = ValidQuery() with
        {
            PageSize = SearchAuditRecordsQuery.MaxPageSize + 1
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SearchAuditRecordsQuery.PageSize));
    }

    [Fact]
    public void Validate_should_reject_page_below_minimum()
    {
        var query = ValidQuery() with
        {
            Page = 0
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SearchAuditRecordsQuery.Page));
    }

    [Fact]
    public void Validate_should_reject_interval_above_limit()
    {
        var query = ValidQuery() with
        {
            To = ValidQuery().From!.Value.AddDays(SearchAuditRecordsQuery.MaxIntervalDays).AddTicks(1)
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_should_reject_to_before_from()
    {
        var query = ValidQuery() with
        {
            To = ValidQuery().From!.Value.AddTicks(-1)
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_should_reject_string_filters_above_limits()
    {
        var query = ValidQuery() with
        {
            SourceService = new string('a', FunctionalAuditRecord.SourceServiceMaxLength + 1)
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SearchAuditRecordsQuery.SourceService));
    }

    private static SearchAuditRecordsQuery ValidQuery()
        => new(
            MerchantId: "m1",
            SourceService: null,
            OperationType: null,
            Status: null,
            EntityType: null,
            EntityId: null,
            From: DateTimeOffset.Parse("2026-06-01T00:00:00Z", CultureInfo.InvariantCulture),
            To: DateTimeOffset.Parse("2026-06-30T00:00:00Z", CultureInfo.InvariantCulture));
}
