namespace AuditService.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void Domain_assembly_should_use_audit_service_name()
    {
        Assert.Equal("AuditService.Domain", System.Reflection.Assembly.Load("AuditService.Domain").GetName().Name);
    }
}
