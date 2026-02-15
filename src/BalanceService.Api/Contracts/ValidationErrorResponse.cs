namespace BalanceService.Api.Contracts;

/// <summary>
/// Formato de erro retornado quando há falhas de validação de request (HTTP 400).
/// </summary>
/// <remarks>
/// Este formato é gerado pelo <c>GlobalExceptionHandler</c> quando uma <c>FluentValidation.ValidationException</c>
/// é lançada.
/// </remarks>
public sealed class ValidationErrorResponse
{
    /// <summary>
    /// Identificador do tipo de erro (link para documentação/HTTP status).
    /// </summary>
    public string Type { get; init; } = "https://httpstatuses.com/400";

    /// <summary>
    /// Título curto do erro.
    /// </summary>
    public string Title { get; init; } = "Invalid request";

    /// <summary>
    /// Código HTTP retornado.
    /// </summary>
    public int Status { get; init; } = 400;

    /// <summary>
    /// Mensagem de detalhe.
    /// </summary>
    public string Detail { get; init; } = "One or more validation errors occurred.";

    /// <summary>
    /// Dicionário de erros por campo, onde a chave é o nome do campo em camelCase.
    /// </summary>
    public Dictionary<string, string[]> Errors { get; init; } = new();

    /// <summary>
    /// CorrelationId associado à requisição (mesmo valor retornado no header <c>X-Correlation-Id</c>).
    /// </summary>
    public string? CorrelationId { get; init; }
}
