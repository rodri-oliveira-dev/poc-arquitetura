using System.Reflection;
using System.Threading.RateLimiting;

using ApiDefaults.Middlewares;
using ApiDefaults.Options;
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
    public const string RateLimitPolicyName = "fixed";

    public static IServiceCollection AddApiDefaults<TExceptionHandler>(
        this IServiceCollection services,
        IConfiguration configuration,
        params string[] allowedForwardedHosts)
        where TExceptionHandler : class, IExceptionHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(allowedForwardedHosts);

        services.AddExceptionHandler<TExceptionHandler>();
        services.AddProblemDetails();
        services
            .AddOptions<TrustedForwardedHeadersOptions>()
            .Bind(configuration.GetSection(TrustedForwardedHeadersOptions.SectionName))
            .PostConfigure(options =>
            {
                foreach (string host in allowedForwardedHosts)
                {
                    options.AllowedHosts.Add(host);
                }
            })
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
            .ValidateOnStart();

        AddRateLimiting(services, configuration);
        AddCors(services);
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
            options.AddFixedWindowLimiter(RateLimitPolicyName, config =>
            {
                config.PermitLimit = apiLimits.RateLimitPermitLimit;
                config.Window = TimeSpan.FromSeconds(apiLimits.RateLimitWindowSeconds);
                config.QueueLimit = apiLimits.RateLimitQueueLimit;
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });
    }

    private static void AddCors(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "https://localhost:3001",
                        "https://localhost:5173")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", CorrelationIdMiddleware.HeaderName)
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });
    }

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
