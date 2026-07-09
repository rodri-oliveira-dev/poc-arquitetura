using ApiDefaults.Extensions;

using PaymentService.Api.Security;

namespace PaymentService.Api.Extensions;

public static class JwtAuthServiceCollectionExtensions
{
    public static IServiceCollection AddApiJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
        => services.AddConfiguredApiJwtBearerAuthentication<IMerchantAuthorizationService, MerchantAuthorizationService>(
            configuration,
            environment,
            "Jwt",
            static options => options.AddScopePolicies().RequireAuthenticatedUserByDefault());
}
