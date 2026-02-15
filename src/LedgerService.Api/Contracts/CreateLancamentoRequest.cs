namespace LedgerService.Api.Contracts;

/// <summary>
/// Request para criação de um lançamento no ledger.
/// </summary>
/// <remarks>
/// Observação: a idempotência e correlação são tratadas via headers.
/// - <c>Idempotency-Key</c> (obrigatório, UUID)
/// - <c>X-Correlation-Id</c> (opcional, UUID)
/// </remarks>
/// <param name="MerchantId">Identificador do merchant/lojista ao qual o lançamento pertence.</param>
/// <param name="Type">
/// Tipo do lançamento. Valores aceitos (case-insensitive): <c>CREDIT</c> ou <c>DEBIT</c>.
/// </param>
/// <param name="Amount">
/// Valor monetário do lançamento, em formato decimal (InvariantCulture). Regras: para <c>CREDIT</c>, deve ser &gt; 0;
/// para <c>DEBIT</c>, deve ser &lt; 0; nunca pode ser 0.
/// </param>
/// <param name="Description">Descrição opcional do lançamento.</param>
/// <param name="ExternalReference">Referência externa opcional (ex.: id do sistema origem).</param>
public sealed record CreateLancamentoRequest(
    string MerchantId,
    string Type,
    double Amount,
    string? Description,
    string? ExternalReference);