using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Gateway;

namespace PaymentService.Infrastructure.Gateway;

public sealed partial class FakePaymentGateway(
    IOptions<PaymentGatewayOptions> options,
    PaymentGatewayTelemetry telemetry,
    ILogger<FakePaymentGateway> logger) : IPaymentGateway
{
    private readonly FakePaymentGatewayOptions _options = options.Value.Fake;

    public async Task<CreateExternalPaymentResult> CreatePaymentIntentAsync(
        CreateExternalPaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = telemetry.StartCreateActivity("fake");
        var start = TimeProvider.System.GetTimestamp();

        if (_options.SimulatedDelay > TimeSpan.Zero)
            await Task.Delay(_options.SimulatedDelay, cancellationToken);

        try
        {
            var result = _options.Scenario switch
            {
                FakePaymentGatewayScenarios.Success => Success(request, "requires_payment_method", requiresAction: true),
                FakePaymentGatewayScenarios.RequiresAction => Success(request, "requires_action", requiresAction: true),
                FakePaymentGatewayScenarios.Processing => Success(request, "processing", requiresAction: false),
                FakePaymentGatewayScenarios.DefinitiveFailure => throw Failure(PaymentGatewayErrorCategory.PaymentRejected, "fake_payment_rejected"),
                FakePaymentGatewayScenarios.Timeout => throw Failure(PaymentGatewayErrorCategory.UnknownResult, "fake_timeout"),
                FakePaymentGatewayScenarios.RateLimit => throw Failure(PaymentGatewayErrorCategory.RateLimited, "fake_rate_limited", TimeSpan.FromSeconds(1)),
                FakePaymentGatewayScenarios.TransientFailure => throw Failure(PaymentGatewayErrorCategory.Transient, "fake_transient"),
                _ => throw Failure(PaymentGatewayErrorCategory.InvalidRequest, "fake_scenario_invalid")
            };

            telemetry.RecordSuccess("fake", Elapsed(start));
            LogFakeGatewaySuccess(logger, _options.Scenario);
            return result;
        }
        catch (PaymentGatewayException ex)
        {
            telemetry.RecordFailure("fake", ex.Category, Elapsed(start));
            LogFakeGatewayFailure(logger, _options.Scenario, ex.Category);
            throw;
        }
    }

    public async Task<CreateExternalRefundResult> CreateRefundAsync(
        CreateExternalRefundRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = telemetry.StartCreateActivity("fake");
        var start = TimeProvider.System.GetTimestamp();

        if (_options.SimulatedDelay > TimeSpan.Zero)
            await Task.Delay(_options.SimulatedDelay, cancellationToken);

        try
        {
            var result = _options.Scenario switch
            {
                FakePaymentGatewayScenarios.Timeout => throw Failure(PaymentGatewayErrorCategory.UnknownResult, "fake_refund_timeout"),
                FakePaymentGatewayScenarios.RateLimit => throw Failure(PaymentGatewayErrorCategory.RateLimited, "fake_refund_rate_limited", TimeSpan.FromSeconds(1)),
                FakePaymentGatewayScenarios.TransientFailure => throw Failure(PaymentGatewayErrorCategory.Transient, "fake_refund_transient"),
                FakePaymentGatewayScenarios.DefinitiveFailure or FakePaymentGatewayScenarios.RefundFailed => throw Failure(PaymentGatewayErrorCategory.InvalidRequest, "fake_refund_failed"),
                FakePaymentGatewayScenarios.RefundPending => RefundSuccess(request, "pending"),
                _ => RefundSuccess(request, "succeeded")
            };

            telemetry.RecordSuccess("fake", Elapsed(start));
            LogFakeGatewaySuccess(logger, _options.Scenario);
            return result;
        }
        catch (PaymentGatewayException ex)
        {
            telemetry.RecordFailure("fake", ex.Category, Elapsed(start));
            LogFakeGatewayFailure(logger, _options.Scenario, ex.Category);
            throw;
        }
    }

    private CreateExternalPaymentResult Success(CreateExternalPaymentRequest request, string status, bool requiresAction)
    {
        var suffix = request.PaymentId.ToString("N")[..12];
        return new CreateExternalPaymentResult(
            "Fake",
            $"{_options.ProviderPaymentIdPrefix}_{suffix}",
            status,
            $"pi_fake_secret_{suffix}",
            requiresAction,
            status);
    }

    private static CreateExternalRefundResult RefundSuccess(CreateExternalRefundRequest request, string status)
    {
        var suffix = request.RefundId.ToString("N")[..12];
        return new CreateExternalRefundResult(
            "Fake",
            $"re_fake_{suffix}",
            request.ProviderPaymentId,
            status,
            request.Amount,
            request.Currency,
            DateTimeOffset.UtcNow,
            status);
    }

    private static PaymentGatewayException Failure(
        PaymentGatewayErrorCategory category,
        string code,
        TimeSpan? retryAfter = null)
        => new(category, "Falha simulada pelo provider fake.", code, retryAfter);

    private static long Elapsed(long start)
        => (long)TimeProvider.System.GetElapsedTime(start).TotalMilliseconds;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Provider fake criou intencao externa. Scenario: {Scenario}")]
    private static partial void LogFakeGatewaySuccess(ILogger logger, string scenario);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Provider fake falhou ao criar intencao externa. Scenario: {Scenario}; Category: {Category}")]
    private static partial void LogFakeGatewayFailure(ILogger logger, string scenario, PaymentGatewayErrorCategory category);
}
