using Asp.Versioning.ApiExplorer;

using IdentityService.Api.Extensions;
using IdentityService.Api.Security;
using IdentityService.Api.Swagger;
using IdentityService.Application.Idempotency;
using IdentityService.Application.Users.Commands;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityService.UnitTests.Api.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIdentityApiComposition_should_register_api_services_and_scope_policies()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "http://localhost:8081/realms/poc",
                ["Jwt:Audience"] = "identity-api",
                ["Jwt:JwksUrl"] = "http://localhost:8081/realms/poc/protocol/openid-connect/certs",
                ["Jwt:RequireHttpsMetadata"] = "false"
            })
            .Build();

        services.AddSingleton<IWebHostEnvironment>(new FakeHostEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddSingleton<IApiVersionDescriptionProvider>(new FakeApiVersionDescriptionProvider());

        services.AddIdentityApiComposition(configuration, new FakeHostEnvironment());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(CreateUserCommandHandler)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IIdempotencyService)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IIdempotencyRequestHasher)
                && descriptor.Lifetime == ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        var swaggerOptions = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        Assert.Contains(
            swaggerOptions.OperationFilterDescriptors,
            descriptor => descriptor.Type == typeof(AuthorizeOperationFilter));
        Assert.Contains(
            swaggerOptions.OperationFilterDescriptors,
            descriptor => descriptor.Type == typeof(CreateUserIdempotencyHeaderOperationFilter));

        var authorizationOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        Assert.NotNull(authorizationOptions.GetPolicy(ScopePolicies.IdentityWritePolicy));
        Assert.NotNull(authorizationOptions.GetPolicy(ScopePolicies.IdentityReadPolicy));
    }

    [Fact]
    public void AddIdentityApiComposition_should_validate_arguments()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddIdentityApiComposition(null!, configuration, environment));
        Assert.Throws<ArgumentNullException>(() => services.AddIdentityApiComposition(null!, environment));
        Assert.Throws<ArgumentNullException>(() => services.AddIdentityApiComposition(configuration, null!));
    }

    private sealed class FakeApiVersionDescriptionProvider : IApiVersionDescriptionProvider
    {
        public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions => [];
    }

    private sealed class FakeHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName
        {
            get;
            set;
        } = Environments.Development;

        public string ApplicationName
        {
            get;
            set;
        } = "IdentityService.Api";

        public string ContentRootPath
        {
            get;
            set;
        } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider
        {
            get;
            set;
        } = new NullFileProvider();

        public string WebRootPath
        {
            get;
            set;
        } = Directory.GetCurrentDirectory();

        public IFileProvider WebRootFileProvider
        {
            get;
            set;
        } = new NullFileProvider();
    }
}
