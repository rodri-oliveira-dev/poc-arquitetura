using AuditService.Application.FunctionalAuditing.CreateAuditRecord;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed class AuditRecordIngestionService(
    IAuditRecordValidator validator,
    IAuditRecordMapper mapper,
    ISender sender) : IAuditRecordIngestionService
{
    private readonly IAuditRecordValidator _validator = validator;
    private readonly IAuditRecordMapper _mapper = mapper;
    private readonly ISender _sender = sender;

    public Task<CreateAuditRecordResult> IngestAsync(
        AuditRecordEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        _validator.ValidateAndThrow(envelope);
        CreateAuditRecordCommand command = _mapper.Map(envelope);

        return _sender.Send(command, cancellationToken);
    }
}
