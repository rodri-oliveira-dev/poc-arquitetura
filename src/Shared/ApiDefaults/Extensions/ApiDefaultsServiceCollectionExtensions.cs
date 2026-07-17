using System.Reflection;
using System.Threading.RateLimiting;

using ApiDefaults.Options;
using ApiDefaults.RateLimiting;
using ApiDefaults.Swagger;

using Asp.Versioning;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiDefaults.Extensions;

public static class ApiDefaultsServiceCollectionExtensions
{
    public const string CorsPolicyName = "ApiCorsPolicy";
    public const string RateLimitPolicyName = ApiRateLimitPolicies.LegacyFixed;

    public static IServiceCollection AddApiDefaults<TExceptionHandler>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TExceptionHandler : class, IExceptionHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddExceptionHandler<TExceptionHandler>();
        services.AddProblemDetails();
        services
            .AddOptions<TrustedForwardedHeadersOptions>()
            .Bind(configuration.GetSection(TrustedForwardedHeadersOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TrustedForwardedHeadersOptions>, TrustedForwardedHeadersOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<ForwardedHeadersOptions>, TrustedForwardedHeadersPostConfigureOptions>());
        services
            .AddOptions<ApiDefaultsOptions>()
            .Bind(configuration.GetSection(ApiDefaultsOptions.SectionName))
            .Validate(options => options.MaxRequestBodySizeBytes > 0, "ApiLimits:MaxRequestBodySizeBytes must be greater than zero.")
            .Validate(options => options.RateLimitPermitLimit > 0, "ApiLimits:RateLimitPermitLimit must be greater than zero.")
            .Validate(options => options.RateLimitWindowSeconds > 0, "ApiLimits:RateLimitWindowSeconds must be greater than zero.")
            .Validate(options => options.RateLimitQueueLimit >= 0, "ApiLimits:RateLimitQueueLimit must be zero or greater.")
            .Validate(HasValidRateLimitPolicies, "ApiLimits policy rate limits must have PermitLimit greater than zero, WindowSeconds greater than zero and QueueLimit zero or greater when configured.")
            .ValidateOnStart();
        services
            .AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>, CorsPolicyPostConfigureOptions>());

        AddRateLimiting(services, configuration);
        services.AddSingleton<ApiRateLimitMetrics>();
        services.AddCors();
        AddVersioningAndExplorer(services);

        return services;
    }

    public static IServiceCollection AddApiSwaggerDefaults<TConfigureSwaggerOptions>(
        this IServiceCollection services,
        Assembly apiAssembly,
        Action<SwaggerGenOptions> configureSpecificDefaults)
        where TConfigureSwaggerOptions : class, IConfigureOptions<SwaggerGenOptions>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(apiAssembly);
        ArgumentNullException.ThrowIfNull(configureSpecificDefaults);

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, TConfigureSwaggerOptions>();
        services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();
            options.DocumentFilter<OpenApiContractQualityDocumentFilter>();
            options.DocInclusionPredicate((docName, apiDescription) =>
                string.Equals(apiDescription.GroupName, docName, StringComparison.OrdinalIgnoreCase));

            string xmlPath = Path.Combine(AppContext.BaseDirectory, $"{apiAssembly.GetName().Name}.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: false);
            }

            configureSpecificDefaults(options);
        });

        return services;
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        ApiDefaultsOptions apiLimits = configuration.GetSection(ApiDefaultsOptions.SectionName).Get<ApiDefaultsOptions>()
            ?? new ApiDefaultsOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                string policy = context.HttpContext.GetEndpoint()?.Metadata
                    .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                    ?? "unknown";
                string partitionType = string.Equals(policy, ApiRateLimitPolicies.AnonymousWebhook, StringComparison.Ordinal)
                        ? "anonymous_ip"
                        : ApiRateLimitPartitionKeyFactory.DescribeAuthenticatedPartitionType(context.HttpContext);

                ApiRateLimitMetrics metrics = context.HttpContext.RequestServices.GetRequiredService<ApiRateLimitMetrics>();
                metrics.RecordRejected(policy, partitionType);

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await ValueTask.CompletedTask;
            };

            AddAuthenticatedPolicy(
                options,
                ApiRateLimitPolicies.AuthenticatedRead,
                ResolvePolicyOptions(apiLimits, apiLimits.AuthenticatedReadRateLimit));
            AddAuthenticatedPolicy(
                options,
                ApiRateLimitPolicies.AuthenticatedWrite,
                ResolvePolicyOptions(apiLimits, apiLimits.AuthenticatedWriteRateLimit));
            AddAuthenticatedPolicy(
                options,
                ApiRateLimitPolicies.Administrative,
                ResolvePolicyOptions(apiLimits, apiLimits.AdministrativeRateLimit));
            AddAnonymousIpPolicy(
                options,
                ApiRateLimitPolicies.AnonymousWebhook,
                ResolvePolicyOptions(apiLimits, apiLimits.AnonymousWebhookRateLimit));
            AddAuthenticatedPolicy(
                options,
                ApiRateLimitPolicies.LegacyFixed,
                ResolvePolicyOptions(apiLimits, apiLimits.AuthenticatedWriteRateLimit));
        });
    }

    private static void AddAuthenticatedPolicy(
        RateLimiterOptions options,
        string policyName,
        ResolvedRateLimitPolicyOptions policyOptions)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ApiRateLimitPartitionKeyFactory.CreateAuthenticatedKey(httpContext),
                _ => CreateFixedWindowOptions(policyOptions)));
    }

    private static void AddAnonymousIpPolicy(
        RateLimiterOptions options,
        string policyName,
        ResolvedRateLimitPolicyOptions policyOptions)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ApiRateLimitPartitionKeyFactory.CreateAnonymousIpKey(httpContext),
                _ => CreateFixedWindowOptions(policyOptions)));
    }

    private static FixedWindowRateLimiterOptions CreateFixedWindowOptions(ResolvedRateLimitPolicyOptions policyOptions)
        => new()
        {
            PermitLimit = policyOptions.PermitLimit,
            Window = TimeSpan.FromSeconds(policyOptions.WindowSeconds),
            QueueLimit = policyOptions.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };

    private static ResolvedRateLimitPolicyOptions ResolvePolicyOptions(
        ApiDefaultsOptions apiLimits,
        RateLimitPolicyOptions policyOptions)
        => new(
            policyOptions.PermitLimit ?? apiLimits.RateLimitPermitLimit,
            policyOptions.WindowSeconds ?? apiLimits.RateLimitWindowSeconds,
            policyOptions.QueueLimit ?? apiLimits.RateLimitQueueLimit);

    private static bool HasValidRateLimitPolicies(ApiDefaultsOptions options)
        => HasValidRateLimitPolicy(options.AuthenticatedReadRateLimit)
            && HasValidRateLimitPolicy(options.AuthenticatedWriteRateLimit)
            && HasValidRateLimitPolicy(options.AdministrativeRateLimit)
            && HasValidRateLimitPolicy(options.AnonymousWebhookRateLimit);

    private static bool HasValidRateLimitPolicy(RateLimitPolicyOptions options)
        => options.PermitLimit is null or > 0
            && options.WindowSeconds is null or > 0
            && options.QueueLimit is null or >= 0;

    private readonly record struct ResolvedRateLimitPolicyOptions(
        int PermitLimit,
        int WindowSeconds,
        int QueueLimit);

    private static void AddVersioningAndExplorer(IServiceCollection services)
    {
        IApiVersioningBuilder versioningBuilder = services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc();

        versioningBuilder.AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
    }
}
