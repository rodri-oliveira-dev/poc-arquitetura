using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Gateway;

namespace PaymentService.Infrastructure.Gateway;

public sealed partial class StripePaymentGateway(
    HttpClient httpClient,
    IOptions<PaymentGatewayOptions> options,
    PaymentGatewayTelemetry telemetry,
    ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    private const string ProviderName = "stripe";
    private readonly StripePaymentGatewayOptions _options = options.Value.Stripe;

    public async Task<CreateExternalPaymentResult> CreatePaymentIntentAsync(
        CreateExternalPaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = telemetry.StartCreateActivity(ProviderName);
        var start = TimeProvider.System.GetTimestamp();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payment_intents")
        {
            Content = new FormUrlEncodedContent(CreateFormFields(request))
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);

        try
        {
            LogStripeCreateStarted(logger);
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var exception = MapHttpFailure(response.StatusCode, responseBody, response.Headers.RetryAfter);
                telemetry.RecordFailure(ProviderName, exception.Category, Elapsed(start));
                LogStripeCreateFailed(logger, exception.Category, exception.Code);
                throw exception;
            }

            var result = ParsePaymentIntent(responseBody);
            telemetry.RecordSuccess(ProviderName, Elapsed(start));
            LogStripeCreateSucceeded(logger, result.ProviderStatus);
            return result;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var exception = new PaymentGatewayException(
                PaymentGatewayErrorCategory.UnknownResult,
                "Timeout ao criar PaymentIntent na Stripe.",
                "stripe_timeout",
                innerException: ex);
            telemetry.RecordFailure(ProviderName, exception.Category, Elapsed(start));
            LogStripeCreateFailed(logger, exception.Category, exception.Code);
            throw exception;
        }
        catch (HttpRequestException ex)
        {
            var exception = new PaymentGatewayException(
                PaymentGatewayErrorCategory.Transient,
                "Falha transitoria de rede ao criar PaymentIntent na Stripe.",
                "stripe_network_error",
                innerException: ex);
            telemetry.RecordFailure(ProviderName, exception.Category, Elapsed(start));
            LogStripeCreateFailed(logger, exception.Category, exception.Code);
            throw exception;
        }
        catch (Exception ex) when (IsCircuitOpen(ex))
        {
            var exception = new PaymentGatewayException(
                PaymentGatewayErrorCategory.CircuitOpen,
                "Circuit breaker aberto para a Stripe.",
                "stripe_circuit_open",
                innerException: ex);
            telemetry.RecordFailure(ProviderName, exception.Category, Elapsed(start));
            LogStripeCreateFailed(logger, exception.Category, exception.Code);
            throw exception;
        }
    }

    private static List<KeyValuePair<string, string>> CreateFormFields(CreateExternalPaymentRequest request)
    {
        var amountInMinorUnits = checked((long)(request.Amount * 100m));
        List<KeyValuePair<string, string>> fields =
        [
            new("amount", amountInMinorUnits.ToString(CultureInfo.InvariantCulture)),
            new("currency", request.Currency.ToLowerInvariant()),
            new("automatic_payment_methods[enabled]", "true"),
            new("metadata[payment_id]", request.PaymentId.ToString("D")),
            new("metadata[merchant_id]", request.MerchantId)
        ];

        if (!string.IsNullOrWhiteSpace(request.Description))
            fields.Add(new("description", request.Description));

        if (!string.IsNullOrWhiteSpace(request.ExternalReference))
            fields.Add(new("metadata[external_reference]", request.ExternalReference));

        return fields;
    }

    private static CreateExternalPaymentResult ParsePaymentIntent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var id = GetRequiredString(root, "id");
        var status = GetRequiredString(root, "status");
        var clientSecret = root.TryGetProperty("client_secret", out var clientSecretProperty)
            ? clientSecretProperty.GetString()
            : null;

        return new CreateExternalPaymentResult(
            "Stripe",
            id,
            status,
            clientSecret,
            IsActionRequired(status),
            status);
    }

    private static PaymentGatewayException MapHttpFailure(
        HttpStatusCode statusCode,
        string responseBody,
        RetryConditionHeaderValue? retryAfterHeader)
    {
        var code = TryReadStripeErrorCode(responseBody) ?? $"stripe_http_{(int)statusCode}";
        var retryAfter = retryAfterHeader?.Delta;

#pragma warning disable IDE0072 // Status HTTP nao mapeado deve cair em Unknown.
        var category = statusCode switch
        {
            HttpStatusCode.RequestTimeout => PaymentGatewayErrorCategory.UnknownResult,
            HttpStatusCode.TooManyRequests => PaymentGatewayErrorCategory.RateLimited,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PaymentGatewayErrorCategory.AuthenticationFailed,
            HttpStatusCode.Conflict => PaymentGatewayErrorCategory.Conflict,
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity => PaymentGatewayErrorCategory.InvalidRequest,
            >= HttpStatusCode.InternalServerError => PaymentGatewayErrorCategory.Transient,
            _ => PaymentGatewayErrorCategory.Unknown
        };
#pragma warning restore IDE0072

        return new PaymentGatewayException(
            category,
            "Stripe recusou ou nao concluiu a criacao do PaymentIntent.",
            code,
            retryAfter);
    }

    private static string? TryReadStripeErrorCode(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new PaymentGatewayException(
                PaymentGatewayErrorCategory.Unknown,
                "Stripe retornou PaymentIntent sem campo obrigatorio.",
                $"stripe_missing_{propertyName}");

    private static bool IsActionRequired(string status)
        => status is "requires_payment_method" or "requires_confirmation" or "requires_action";

    private static bool IsCircuitOpen(Exception exception)
        => exception.GetType().Name.Contains("Circuit", StringComparison.OrdinalIgnoreCase)
            && exception.GetType().Name.Contains("Open", StringComparison.OrdinalIgnoreCase);

    private static long Elapsed(long start)
        => (long)TimeProvider.System.GetElapsedTime(start).TotalMilliseconds;

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Criando PaymentIntent na Stripe.")]
    private static partial void LogStripeCreateStarted(ILogger logger);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "PaymentIntent criado na Stripe. ProviderStatus: {ProviderStatus}")]
    private static partial void LogStripeCreateSucceeded(ILogger logger, string providerStatus);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Falha ao criar PaymentIntent na Stripe. Category: {Category}; Code: {Code}")]
    private static partial void LogStripeCreateFailed(ILogger logger, PaymentGatewayErrorCategory category, string? code);
}
