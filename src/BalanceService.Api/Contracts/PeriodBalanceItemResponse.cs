namespace BalanceService.Api.Contracts;

/// <summary>
/// Item diário retornado dentro do consolidado por período.
/// </summary>
public sealed class PeriodBalanceItemResponse
{
    /// <summary>
    /// Data do item no formato ISO-8601 (<c>YYYY-MM-DD</c>).
    /// </summary>
    public required string Date { get; init; }

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
    /// Timestamp mais recente considerado no consolidado daquele dia (ISO-8601).
    /// Quando não há dados para o dia, este campo pode ser <c>null</c>.
    /// </summary>
    public string? AsOf { get; init; }
}
