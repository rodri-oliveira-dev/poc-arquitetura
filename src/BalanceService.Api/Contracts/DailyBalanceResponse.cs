namespace BalanceService.Api.Contracts;

/// <summary>
/// Resposta do consolidado diário para um merchant em uma data específica,
/// derivada da tabela <c>daily_balances</c>.
/// </summary>
public sealed class DailyBalanceResponse
{
    /// <summary>
    /// Identificador do merchant/lojista.
    /// </summary>
    public required string MerchantId { get; init; }

    /// <summary>
    /// Data do consolidado no formato ISO-8601 (<c>YYYY-MM-DD</c>).
    /// </summary>
    public required string Date { get; init; }

    /// <summary>
    /// Moeda (código de 3 letras, ex.: <c>BRL</c>).
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Soma dos créditos do dia (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    public required string TotalCredits { get; init; }

    /// <summary>
    /// Soma dos débitos do dia (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    public required string TotalDebits { get; init; }

    /// <summary>
    /// Saldo líquido do dia (<c>TotalCredits - TotalDebits</c>) (string decimal com 2 casas, InvariantCulture).
    /// </summary>
    public required string NetBalance { get; init; }

    /// <summary>
    /// Timestamp mais recente considerado no consolidado (ISO-8601).
    /// Quando não há dados para o dia, este campo pode ser <c>null</c>.
    /// </summary>
    public string? AsOf { get; init; }

    /// <summary>
    /// Timestamp em que a consulta foi calculada/gerada (ISO-8601).
    /// </summary>
    public required string CalculatedAt { get; init; }
}
