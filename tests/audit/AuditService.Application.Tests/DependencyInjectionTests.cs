using AuditService.Application.FunctionalAuditing.Ingestion;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using ApplicationDependencyInjection = AuditService.Application.DependencyInjection;

namespace AuditService.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAuditApplication_should_register_mediator()
    {
        IServiceCollection services = ApplicationDependencyInjection.AddAuditApplication(
            new ServiceCollection());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMediator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditRecordIngestionService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditRecordMapper));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditRecordValidator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditRecordSerializer));
    }
}
