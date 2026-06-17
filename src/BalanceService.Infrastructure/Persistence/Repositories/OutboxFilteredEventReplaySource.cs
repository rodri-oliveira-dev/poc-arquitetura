using BalanceService.Application.Balances.Replay;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

namespace BalanceService.Infrastructure.Persistence.Repositories;

public sealed class OutboxFilteredEventReplaySource : IFilteredEventReplaySource
{
    private readonly BalanceDbContext _context;

    public OutboxFilteredEventReplaySource(BalanceDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<IReadOnlyList<EventReplaySourceCandidate>> FindAsync(
        FilteredEventReplayFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var boundedLimit = Math.Clamp(limit, 1, 1000);
        var rows = await _context.Database
            .SqlQueryRaw<OutboxReplayRow>(
                """
                SELECT
                    id::text AS "SourceId",
                    payload::text AS "Payload",
                    split_part(event_type, '.', 1) AS "EventName",
                    split_part(event_type, '.', 2) AS "EventVersion",
                    occurred_at AS "OccurredAt",
                    payload ->> 'merchantId' AS "MerchantId",
                    payload ->> 'accountId' AS "AccountId",
                    status AS "Status",
                    event_type AS "EventType",
                    aggregate_type AS "AggregateType",
                    aggregate_id::text AS "AggregateId",
                    correlation_id::text AS "CorrelationId",
                    traceparent AS "TraceParent",
                    tracestate AS "TraceState",
                    baggage AS "Baggage"
                FROM ledger.outbox_messages
                WHERE (@p_event_name IS NULL OR split_part(event_type, '.', 1) = @p_event_name)
                  AND (@p_event_version IS NULL OR split_part(event_type, '.', 2) = @p_event_version)
                  AND (@p_occurred_from IS NULL OR occurred_at >= @p_occurred_from)
                  AND (@p_occurred_until IS NULL OR occurred_at <= @p_occurred_until)
                  AND (@p_merchant_id IS NULL OR payload ->> 'merchantId' = @p_merchant_id)
                  AND (@p_account_id IS NULL OR payload ->> 'accountId' = @p_account_id)
                  AND (@p_status IS NULL OR status = @p_status)
                ORDER BY occurred_at
                LIMIT @p_limit
                """,
                TextParameter("p_event_name", filter.EventName),
                TextParameter("p_event_version", filter.EventVersion),
                TimestampParameter("p_occurred_from", filter.OccurredFrom),
                TimestampParameter("p_occurred_until", filter.OccurredUntil),
                TextParameter("p_merchant_id", filter.MerchantId),
                TextParameter("p_account_id", filter.AccountId),
                TextParameter("p_status", filter.Status),
                new NpgsqlParameter("p_limit", NpgsqlDbType.Integer) { Value = boundedLimit })
            .ToListAsync(cancellationToken);

        return rows.Select(ToCandidate).ToArray();
    }

    private static EventReplaySourceCandidate ToCandidate(OutboxReplayRow row)
        => new(
            new EventReplaySourcePosition(row.SourceId, row.OccurredAt, row.Status),
            new EventReplayPayload(row.Payload, ToMetadata(row)),
            new EventReplayContract(row.EventName, row.EventVersion, "Outbox"),
            new EventReplaySubject(row.MerchantId, row.AccountId));

    private static Dictionary<string, string> ToMetadata(OutboxReplayRow row)
    {
        var metadata = new Dictionary<string, string>
        {
            ["source"] = "ledger.outbox_messages",
            ["outbox_message_id"] = row.SourceId,
            ["event_type"] = row.EventType,
            ["aggregate_type"] = row.AggregateType,
            ["aggregate_id"] = row.AggregateId
        };

        AddIfPresent(metadata, "correlation_id", row.CorrelationId);
        AddIfPresent(metadata, "traceparent", row.TraceParent);
        AddIfPresent(metadata, "tracestate", row.TraceState);
        AddIfPresent(metadata, "baggage", row.Baggage);

        return metadata;
    }

    private static void AddIfPresent(Dictionary<string, string> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            metadata[key] = value;
    }

    private static NpgsqlParameter TextParameter(string name, string? value)
        => new(name, NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value
        };

    private static NpgsqlParameter TimestampParameter(string name, DateTimeOffset? value)
        => new(name, NpgsqlDbType.TimestampTz)
        {
            Value = value?.UtcDateTime ?? (object)DBNull.Value
        };

    public sealed class OutboxReplayRow
    {
        public string SourceId { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
        public string EventName { get; init; } = string.Empty;
        public string EventVersion { get; init; } = string.Empty;
        public DateTimeOffset OccurredAt
        {
            get; init;
        }
        public string? MerchantId
        {
            get; init;
        }
        public string? AccountId
        {
            get; init;
        }
        public string? Status
        {
            get; init;
        }
        public string EventType { get; init; } = string.Empty;
        public string AggregateType { get; init; } = string.Empty;
        public string AggregateId { get; init; } = string.Empty;
        public string? CorrelationId
        {
            get; init;
        }
        public string? TraceParent
        {
            get; init;
        }
        public string? TraceState
        {
            get; init;
        }
        public string? Baggage
        {
            get; init;
        }
    }
}
