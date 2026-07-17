using ApplicationDefaults.Behaviors;

using AuditService.Application.FunctionalAuditing.Ingestion;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace AuditService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAuditRecordMapper, AuditRecordMapper>();
        services.AddSingleton<IAuditRecordSerializer, AuditRecordSerializer>();
        services.AddSingleton<IAuditRecordValidator, AuditRecordValidator>();
        services.AddScoped<IAuditRecordIngestionService, AuditRecordIngestionService>();

        return services;
    }
}
