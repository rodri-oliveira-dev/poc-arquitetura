using FluentAssertions;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Lancamentos.Queries;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using Moq;

namespace LedgerService.UnitTests.Tests;

public sealed class ObterStatusReprocessamentoLancamentosHandlerTests
{
    [Fact]
    public async Task Should_return_status_for_existing_reprocessamento()
    {
        var repo = new Mock<IReprocessamentoLancamentosRepository>(MockBehavior.Strict);
        var reprocessamento = new ReprocessamentoLancamentos(
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            Guid.NewGuid());
        repo.Setup(x => x.GetByIdAsync(reprocessamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reprocessamento);
        var sut = new ObterStatusReprocessamentoLancamentosHandler(repo.Object);

        var result = await sut.Handle(
            new ObterStatusReprocessamentoLancamentosQuery(reprocessamento.Id, ["m1"]),
            CancellationToken.None);

        result.ReprocessamentoId.Should().Be(reprocessamento.Id);
        result.Status.Should().Be("Pending");
        result.Motivo.Should().Be(reprocessamento.Motivo);
    }

    [Fact]
    public async Task Should_throw_not_found_when_reprocessamento_does_not_exist()
    {
        var repo = new Mock<IReprocessamentoLancamentosRepository>(MockBehavior.Strict);
        var id = Guid.NewGuid();
        repo.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReprocessamentoLancamentos?)null);
        var sut = new ObterStatusReprocessamentoLancamentosHandler(repo.Object);

        var act = async () => await sut.Handle(
            new ObterStatusReprocessamentoLancamentosQuery(id, ["m1"]),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_throw_forbidden_when_merchant_is_not_authorized()
    {
        var repo = new Mock<IReprocessamentoLancamentosRepository>(MockBehavior.Strict);
        var reprocessamento = new ReprocessamentoLancamentos(
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra de consolidacao",
            Guid.NewGuid());
        repo.Setup(x => x.GetByIdAsync(reprocessamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reprocessamento);
        var sut = new ObterStatusReprocessamentoLancamentosHandler(repo.Object);

        var act = async () => await sut.Handle(
            new ObterStatusReprocessamentoLancamentosQuery(reprocessamento.Id, ["m2"]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
