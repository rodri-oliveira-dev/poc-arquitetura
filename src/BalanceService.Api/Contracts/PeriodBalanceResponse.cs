namespace BalanceService.Api.Contracts;

/// <summary>
/// Resposta do consolidado por período (agregado + itens diários), derivada de <c>daily_balances</c>.
/// </summary>
public sealed class PeriodBalanceResponse
{
    /// <summary>
    /// Identificador do merchant/lojista.
    /// </summary>
    public required string MerchantId { get; init; }

    /// <summary>
    /// Data inicial do período no formato ISO-8601 (<c>YYYY-MM-DD</c>).
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Data final do período no formato ISO-8601 (<c>YYYY-MM-DD</c>).
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Moeda (código de 3 letras, ex.: <c>BRL</c>).
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Soma de créditos no período (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    public required string TotalCredits { get; init; }

    /// <summary>
    /// Soma de débitos no período (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    public required string TotalDebits { get; init; }

    /// <summary>
    /// Saldo líquido no período (<c>TotalCredits - TotalDebits</c>) (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    public required string NetBalance { get; init; }

    /// <summary>
    /// Lista de itens diários encontrados no intervalo.
    /// </summary>
    public required IReadOnlyList<PeriodBalanceItemResponse> Items { get; init; }

    /// <summary>
    /// Timestamp em que a consulta foi calculada/gerada (ISO-8601).
    /// </summary>
    public required string CalculatedAt { get; init; }
}
