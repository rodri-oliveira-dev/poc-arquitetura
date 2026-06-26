namespace HttpResilienceDefaults;

public sealed class HttpClientResilienceOptions
{
    public const string SectionName = "HttpResilience";

    public bool Enabled { get; set; } = true;
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int RetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public bool RetryUnsafeHttpMethods
    {
        get; set;
    }
    public double CircuitBreakerFailureRatio { get; set; } = 0.1;
    public int CircuitBreakerMinimumThroughput { get; set; } = 100;
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(5);

    public void Validate(string clientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        ValidatePositive(TotalTimeout, clientName, nameof(TotalTimeout));
        ValidatePositive(AttemptTimeout, clientName, nameof(AttemptTimeout));
        ValidatePositive(RetryDelay, clientName, nameof(RetryDelay));
        ValidatePositive(CircuitBreakerSamplingDuration, clientName, nameof(CircuitBreakerSamplingDuration));
        ValidatePositive(CircuitBreakerBreakDuration, clientName, nameof(CircuitBreakerBreakDuration));

        if (AttemptTimeout > TotalTimeout)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Clients:{clientName}:{nameof(AttemptTimeout)} deve ser menor ou igual a {nameof(TotalTimeout)}.");
        }

        if (RetryCount <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Clients:{clientName}:{nameof(RetryCount)} deve ser maior que zero.");
        }

        if (CircuitBreakerFailureRatio is <= 0 or > 1)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Clients:{clientName}:{nameof(CircuitBreakerFailureRatio)} deve ser maior que zero e menor ou igual a um.");
        }

        if (CircuitBreakerMinimumThroughput <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Clients:{clientName}:{nameof(CircuitBreakerMinimumThroughput)} deve ser maior que zero.");
        }
    }

    private static void ValidatePositive(TimeSpan value, string clientName, string optionName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Clients:{clientName}:{optionName} deve ser maior que zero.");
        }
    }
}
