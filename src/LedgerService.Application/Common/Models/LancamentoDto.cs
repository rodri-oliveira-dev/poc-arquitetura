namespace LedgerService.Application.Common.Models;

/// <summary>
/// DTO de resposta representando um lançamento criado.
/// </summary>
public sealed record LancamentoDto(
    /// <summary>
    /// Identificador externo do lançamento (prefixo <c>lan_</c> + fragmento do Guid interno).
    /// </summary>
    string Id,
    /// <summary>
    /// Identificador do merchant/lojista.
    /// </summary>
    string MerchantId,
    /// <summary>
    /// Tipo do lançamento: <c>CREDIT</c> ou <c>DEBIT</c>.
    /// </summary>
    string Type,
    /// <summary>
    /// Valor monetário do lançamento (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    string Amount,
    /// <summary>
    /// Data/hora de ocorrência do lançamento (ISO-8601).
    /// </summary>
    string OccurredAt,
    /// <summary>
    /// Descrição opcional.
    /// </summary>
    string? Description,
    /// <summary>
    /// Referência externa opcional.
    /// </summary>
    string? ExternalReference,
    /// <summary>
    /// Data/hora de criação do lançamento no sistema (ISO-8601).
    /// </summary>
    string CreatedAt);