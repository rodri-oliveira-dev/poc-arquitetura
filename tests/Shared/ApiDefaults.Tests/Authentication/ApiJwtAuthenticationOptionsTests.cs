using ApiDefaults.Authentication;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ApiDefaults.Tests.Authentication;

public sealed class ApiJwtAuthenticationOptionsTests
{
    [Fact]
    public void Validate_should_return_success_for_valid_https_configuration()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_should_return_skip_for_named_options()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate("other", ValidOptions());

        Assert.True(result.Skipped);
    }

    [Theory]
    [InlineData("", "Jwt:Issuer e obrigatorio.")]
    [InlineData("   ", "Jwt:Issuer e obrigatorio.")]
    public void Validate_should_fail_when_issuer_is_empty(string issuer, string expectedMessage)
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            Issuer = issuer
        });

        Assert.True(result.Failed);
        Assert.Contains(expectedMessage, result.Failures);
    }

    [Theory]
    [InlineData("", "Jwt:Audience e obrigatorio.")]
    [InlineData("   ", "Jwt:Audience e obrigatorio.")]
    public void Validate_should_fail_when_audience_is_empty(string audience, string expectedMessage)
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            Audience = audience
        });

        Assert.True(result.Failed);
        Assert.Contains(expectedMessage, result.Failures);
    }

    [Theory]
    [InlineData("", "Jwt:JwksUrl e obrigatorio.")]
    [InlineData("   ", "Jwt:JwksUrl e obrigatorio.")]
    public void Validate_should_fail_when_jwks_url_is_empty(string jwksUrl, string expectedMessage)
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            JwksUrl = jwksUrl
        });

        Assert.True(result.Failed);
        Assert.Contains(expectedMessage, result.Failures);
    }

    [Theory]
    [InlineData("/.well-known/jwks.json")]
    [InlineData("ftp://identity.local/jwks")]
    public void Validate_should_fail_when_jwks_url_is_not_absolute_http_or_https(string jwksUrl)
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Test"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            JwksUrl = jwksUrl
        });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:JwksUrl deve ser uma URI absoluta HTTP ou HTTPS.", result.Failures);
    }

    [Fact]
    public void Validate_should_allow_http_jwks_url_in_test_environment()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Test"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            JwksUrl = "http://identity.local/jwks"
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_should_fail_when_http_metadata_is_used_outside_local_environments()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            JwksUrl = "http://identity.local/jwks"
        });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:JwksUrl deve usar HTTPS fora de Development/Local/Test.", result.Failures);
    }

    [Fact]
    public void Validate_should_fail_when_require_https_metadata_is_false_outside_local_environments()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            RequireHttpsMetadata = false
        });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:RequireHttpsMetadata=false e permitido apenas em Development/Local/Test.", result.Failures);
    }

    [Fact]
    public void Validate_should_allow_require_https_metadata_false_in_local_environment()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Local"));

        ValidateOptionsResult result = validator.Validate(
            null,
            ValidOptions() with
            {
                RequireHttpsMetadata = false,
                JwksUrl = "http://identity.local/jwks"
            });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(ApiJwtAuthenticationOptionsValidator.MaxClockSkewSeconds)]
    public void Validate_should_allow_valid_clock_skew(int clockSkewSeconds)
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            ClockSkewSeconds = clockSkewSeconds
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_should_fail_when_clock_skew_is_negative()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, ValidOptions() with
        {
            ClockSkewSeconds = -1
        });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:ClockSkewSeconds nao pode ser negativo.", result.Failures);
    }

    [Fact]
    public void Validate_should_fail_when_clock_skew_is_excessive()
    {
        var validator = new ApiJwtAuthenticationOptionsValidator(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(
            null,
            ValidOptions() with
            {
                ClockSkewSeconds = ApiJwtAuthenticationOptionsValidator.MaxClockSkewSeconds + 1
            });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:ClockSkewSeconds deve ser menor ou igual a 300.", result.Failures);
    }

    [Fact]
    public void Options_record_should_keep_default_clock_skew()
    {
        ApiJwtAuthenticationOptions options = ValidOptions();

        Assert.Equal(30, options.ClockSkewSeconds);
        Assert.Equal("Jwt", options.SectionName);
        Assert.Equal("https://issuer.example", options.Issuer);
        Assert.Equal("api://payments", options.Audience);
        Assert.Equal("https://issuer.example/.well-known/jwks.json", options.JwksUrl);
        Assert.True(options.RequireHttpsMetadata);
    }

    internal static ApiJwtAuthenticationOptions ValidOptions()
        => new(
            "Jwt",
            "https://issuer.example",
            "api://payments",
            "https://issuer.example/.well-known/jwks.json",
            RequireHttpsMetadata: true,
            JwksTimeoutSeconds: 5,
            JwksRetryCount: 2,
            JwksRetryBaseDelayMilliseconds: 200);

    internal sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "ApiDefaults.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
