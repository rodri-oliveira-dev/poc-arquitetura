using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class EstornoLancamento : Entity, IAggregateRoot
{
    public Guid LancamentoOriginalId { get; private set; }
    public string MerchantId { get; private set; }
    public string Motivo { get; private set; }
    public EstornoLancamentoStatus Status { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private EstornoLancamento()
    {
        MerchantId = string.Empty;
        Motivo = string.Empty;
    }

    public EstornoLancamento(
        Guid lancamentoOriginalId,
        string merchantId,
        string motivo,
        Guid correlationId)
    {
        if (lancamentoOriginalId == Guid.Empty)
            throw new DomainException("LancamentoOriginalId e obrigatorio.");

        LancamentoOriginalId = lancamentoOriginalId;
        MerchantId = string.IsNullOrWhiteSpace(merchantId)
            ? throw new DomainException("MerchantId e obrigatorio.")
            : merchantId.Trim();
        Motivo = string.IsNullOrWhiteSpace(motivo)
            ? throw new DomainException("Motivo e obrigatorio.")
            : motivo.Trim();
        Status = EstornoLancamentoStatus.Pending;
        CorrelationId = correlationId;
        CreatedAt = DateTime.Now;
    }

    public bool IsActive()
        => Status is EstornoLancamentoStatus.Pending or EstornoLancamentoStatus.Processing;
}
