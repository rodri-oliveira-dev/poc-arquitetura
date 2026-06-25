using HttpResilienceDefaults;

namespace TransferService.Worker.Tests.Http;

public sealed class HttpClientResilienceOptionsTests
{
    [Fact]
    public void Defaults_should_be_valid()
    {
        HttpClientResilienceOptions options = new();

        options.Validate("Ledger");
    }

    [Fact]
    public void Validate_should_reject_zero_total_timeout()
    {
        HttpClientResilienceOptions options = new()
        {
            TotalTimeout = TimeSpan.Zero
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("Ledger"));

        Assert.Contains("TotalTimeout", exception.Message, StringComparison.Ordinal);
        Assert.Contains("maior que zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_should_reject_attempt_timeout_greater_than_total_timeout()
    {
        HttpClientResilienceOptions options = new()
        {
            TotalTimeout = TimeSpan.FromSeconds(1),
            AttemptTimeout = TimeSpan.FromSeconds(2)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("JWKS"));

        Assert.Contains("AttemptTimeout", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TotalTimeout", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_should_reject_invalid_circuit_breaker_failure_ratio()
    {
        HttpClientResilienceOptions options = new()
        {
            CircuitBreakerFailureRatio = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("Keycloak"));

        Assert.Contains("CircuitBreakerFailureRatio", exception.Message, StringComparison.Ordinal);
    }
}
