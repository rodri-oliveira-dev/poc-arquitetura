using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts;

[SwaggerSchema(Description = "Payload para criação de um lançamento no ledger.")]
public sealed record CreateLancamentoRequest(
    [property: SwaggerSchema(Description = "Identificador do merchant/lojista ao qual o lançamento pertence.")]
    string MerchantId,
    [property: SwaggerSchema(Description = "Tipo do lançamento. Valores aceitos: `CREDIT` ou `DEBIT`.")]
    string Type,
    [property: SwaggerSchema(Description = "Valor monetário decimal do lançamento, com no máximo 2 casas decimais. Para `CREDIT`, deve ser maior que zero; para `DEBIT`, menor que zero; nunca pode ser zero.")]
    decimal Amount,
    [property: SwaggerSchema(Description = "Descrição opcional do lançamento.")]
    string? Description,
    [property: SwaggerSchema(Description = "Referência externa opcional do sistema de origem.")]
    string? ExternalReference);
