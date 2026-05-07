using LedgerService.Domain.Common;
using LedgerService.Domain.Exceptions;

namespace LedgerService.Domain.Entities;

public sealed class ReprocessamentoLancamentos : Entity, IAggregateRoot
{
    public string MerchantId { get; private set; }
    public DateOnly DataInicial { get; private set; }
    public DateOnly DataFinal { get; private set; }
    public string Motivo { get; private set; }
    public ReprocessamentoLancamentosStatus Status { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ReprocessamentoLancamentos()
    {
        MerchantId = string.Empty;
        Motivo = string.Empty;
    }

    public ReprocessamentoLancamentos(
        string merchantId,
        DateOnly dataInicial,
        DateOnly dataFinal,
        string motivo,
        Guid correlationId)
    {
        if (dataFinal < dataInicial)
            throw new DomainException("DataFinal nao pode ser menor que DataInicial.");

        MerchantId = string.IsNullOrWhiteSpace(merchantId)
            ? throw new DomainException("MerchantId e obrigatorio.")
            : merchantId.Trim();
        DataInicial = dataInicial;
        DataFinal = dataFinal;
        Motivo = string.IsNullOrWhiteSpace(motivo)
            ? throw new DomainException("Motivo e obrigatorio.")
            : motivo.Trim();
        Status = ReprocessamentoLancamentosStatus.Pending;
        CorrelationId = correlationId;
        CreatedAt = DateTime.Now;
    }
}
