using System.Globalization;
using System.Reflection;
using System.Text.Json;

using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Application.FunctionalAuditing.Ingestion;

using FluentValidation;

using MediatR;

namespace AuditService.Application.Tests.FunctionalAuditing;

public sealed class AuditRecordIngestionTests
{
    [Fact]
    public void Mapper_should_convert_canonical_envelope_to_create_audit_record_command()
    {
        var mapper = new AuditRecordMapper();

        CreateAuditRecordCommand command = mapper.Map(ValidEnvelope());

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), command.OperationId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), command.CorrelationId);
        Assert.Equal("00000000-0000-0000-0000-000000000003", command.IdempotencyKey);
        Assert.Equal("AnyCaller", command.SourceService);
        Assert.Equal("FunctionalAuditRecorded", command.OperationType);
        Assert.Equal("Payment", command.EntityType);
        Assert.Equal("pay_123", command.EntityId);
        Assert.Equal("mrc_123", command.MerchantId);
        Assert.NotNull(command.Actor);
        Assert.Equal("Client", command.Actor.Type);
        Assert.Equal("Succeeded", command.Status);
        Assert.Equal("api", command.Metadata!["channel"]);
    }

    [Fact]
    public void Serializer_should_roundtrip_canonical_envelope_with_web_json_names()
    {
        var serializer = new AuditRecordSerializer();

        string json = serializer.Serialize(ValidEnvelope());
        AuditRecordEnvelope result = serializer.Deserialize(json);

        Assert.Contains("\"contractName\"", json);
        Assert.Equal("AuditRecordRequested", result.ContractName);
        Assert.Equal(1, result.ContractVersion);
        Assert.Equal("AnyCaller", result.Payload.SourceService);
        Assert.Equal("api", result.Payload.Metadata!["channel"]);
    }

    [Fact]
    public void Serializer_should_reject_blank_json()
    {
        var serializer = new AuditRecordSerializer();

        Assert.Throws<JsonException>(() => serializer.Deserialize(" "));
    }

    [Fact]
    public void Validator_should_reject_invalid_envelope_shape()
    {
        var validator = new AuditRecordValidator();
        var invalid = ValidEnvelope() with
        {
            ContractName = " ",
            ContractVersion = 0,
            IdempotencyKey = "not-a-uuid",
            Payload = ValidEnvelope().Payload with
            {
                OperationId = Guid.Empty,
                SourceService = "",
                OperationType = "",
                Status = "",
                OccurredAt = default
            },
            Metadata = new AuditMetadata(Guid.Empty, null, null)
        };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateAndThrow(invalid));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(AuditRecordEnvelope.ContractName));
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(AuditRecordPayload.OperationId));
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(AuditMetadata.CorrelationId));
    }

    [Fact]
    public async Task Ingestion_service_should_delegate_to_create_audit_record_use_case()
    {
        var sender = new RecordingSender();
        var service = new AuditRecordIngestionService(
            new AuditRecordValidator(),
            new AuditRecordMapper(),
            sender);

        CreateAuditRecordResult result = await service.IngestAsync(
            ValidEnvelope(),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecordingSender.ResultId, result.Id);
        CreateAuditRecordCommand command = Assert.IsType<CreateAuditRecordCommand>(sender.Request);
        Assert.Equal("AnyCaller", command.SourceService);
        Assert.Equal("FunctionalAuditRecorded", command.OperationType);
    }

    [Fact]
    public void Canonical_contract_should_not_reference_financial_bounded_context_types()
    {
        Assembly assembly = typeof(AuditRecordEnvelope).Assembly;

        string[] referencedAssemblies = [.. assembly.GetReferencedAssemblies().Select(name => name.Name ?? "")];
        string[] contractTypeNames =
        [
            typeof(AuditRecordEnvelope).FullName!,
            typeof(AuditRecordPayload).FullName!,
            typeof(AuditActor).FullName!,
            typeof(AuditMetadata).FullName!
        ];

        Assert.DoesNotContain(referencedAssemblies, name => name.StartsWith("LedgerService.", StringComparison.Ordinal));
        Assert.DoesNotContain(referencedAssemblies, name => name.StartsWith("BalanceService.", StringComparison.Ordinal));
        Assert.DoesNotContain(referencedAssemblies, name => name.StartsWith("TransferService.", StringComparison.Ordinal));
        Assert.DoesNotContain(contractTypeNames, name => name.Contains("Ledger", StringComparison.Ordinal));
        Assert.DoesNotContain(contractTypeNames, name => name.Contains("Balance", StringComparison.Ordinal));
        Assert.DoesNotContain(contractTypeNames, name => name.Contains("Transfer", StringComparison.Ordinal));
    }

    private static AuditRecordEnvelope ValidEnvelope()
        => new(
            ContractName: "AuditRecordRequested",
            ContractVersion: 1,
            IdempotencyKey: "00000000-0000-0000-0000-000000000003",
            Payload: new AuditRecordPayload(
                OperationId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                SourceService: "AnyCaller",
                OperationType: "FunctionalAuditRecorded",
                EntityType: "Payment",
                EntityId: "pay_123",
                MerchantId: "mrc_123",
                Actor: new AuditActor("Client", "subject-123", "any-caller-api"),
                Status: "Succeeded",
                Reason: "Functional operation recorded by caller.",
                Metadata: new Dictionary<string, string>
                {
                    ["channel"] = "api",
                    ["riskLevel"] = "low"
                },
                OccurredAt: DateTimeOffset.Parse("2026-07-01T10:15:30Z", CultureInfo.InvariantCulture)),
            Metadata: new AuditMetadata(
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                CausationId: "request-123",
                Attributes: new Dictionary<string, string>
                {
                    ["adapter"] = "future-http"
                }));

    private sealed class RecordingSender : ISender
    {
        public static readonly Guid ResultId = Guid.Parse("00000000-0000-0000-0000-000000000099");

        public object? Request
        {
            get; private set;
        }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            object result = new CreateAuditRecordResult(ResultId);

            return Task.FromResult((TResponse)result);
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Request = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult<object?>(new CreateAuditRecordResult(ResultId));
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<object?>();
    }
}
