using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using MediatR;
using TransferService.Application;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Transferencias.Commands;

namespace TransferService.UnitTests.Composition;

public sealed class TransferApplicationCompositionTests
{
    [Fact]
    public void AddTransferApplication_should_register_application_services()
    {
        ServiceCollection services = [];

        IServiceCollection result = services.AddTransferApplication();

        Assert.Same(services, result);
        Assert.Contains(services, service => service.ServiceType == typeof(IClock));
        Assert.Contains(services, service => service.ServiceType == typeof(IMediator));
        Assert.Contains(services, service => service.ServiceType == typeof(IValidator<SolicitarTransferenciaCommand>));
    }
}
