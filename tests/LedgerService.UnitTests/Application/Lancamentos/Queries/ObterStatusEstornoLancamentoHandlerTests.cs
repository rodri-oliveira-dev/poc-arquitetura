using System.Reflection;

using FluentAssertions;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Lancamentos.Queries;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using Moq;

namespace LedgerService.UnitTests.Application.Lancamentos.Queries;

public sealed class ObterStatusEstornoLancamentoHandlerTests
{
    [Fact]
    public async Task Should_return_status_for_existing_estorno()
    {
        var estorno = CreateEstorno(EstornoLancamentoStatus.Pending);
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        repo.Setup(x => x.GetByIdAsync(estorno.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(estorno);

        var sut = new ObterStatusEstornoLancamentoHandler(repo.Object);

        var result = await sut.Handle(new ObterStatusEstornoLancamentoQuery(estorno.Id, ["m1"]), CancellationToken.None);

        result.EstornoId.Should().Be(estorno.Id);
        result.LancamentoOriginalId.Should().Be(estorno.LancamentoOriginalId);
        result.Status.Should().Be("Pending");
        result.Motivo.Should().Be(estorno.Motivo);
        result.SolicitadoEm.Should().Be(estorno.CreatedAt);
        result.Should().NotBeEquivalentTo(estorno);
        repo.VerifyAll();
    }

    [Theory]
    [InlineData(EstornoLancamentoStatus.Pending, "Pending")]
    [InlineData(EstornoLancamentoStatus.Processing, "Processing")]
    [InlineData(EstornoLancamentoStatus.Completed, "Completed")]
    [InlineData(EstornoLancamentoStatus.Failed, "Failed")]
    [InlineData(EstornoLancamentoStatus.Rejected, "Rejected")]
    public async Task Should_map_modeled_statuses(EstornoLancamentoStatus status, string expected)
    {
        var estorno = CreateEstorno(status);
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        repo.Setup(x => x.GetByIdAsync(estorno.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(estorno);

        var sut = new ObterStatusEstornoLancamentoHandler(repo.Object);

        var result = await sut.Handle(new ObterStatusEstornoLancamentoQuery(estorno.Id, ["m1"]), CancellationToken.None);

        result.Status.Should().Be(expected);
    }

    [Fact]
    public async Task Should_throw_not_found_when_estorno_does_not_exist()
    {
        var estornoId = Guid.NewGuid();
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        repo.Setup(x => x.GetByIdAsync(estornoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstornoLancamento?)null);

        var sut = new ObterStatusEstornoLancamentoHandler(repo.Object);

        var act = async () => await sut.Handle(new ObterStatusEstornoLancamentoQuery(estornoId, ["m1"]), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*estorno*");
    }

    [Fact]
    public async Task Should_throw_forbidden_when_merchant_is_not_authorized()
    {
        var estorno = CreateEstorno(EstornoLancamentoStatus.Pending);
        var repo = new Mock<IEstornoLancamentoRepository>(MockBehavior.Strict);
        repo.Setup(x => x.GetByIdAsync(estorno.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(estorno);

        var sut = new ObterStatusEstornoLancamentoHandler(repo.Object);

        var act = async () => await sut.Handle(new ObterStatusEstornoLancamentoQuery(estorno.Id, ["m2"]), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*merchant*");
    }

    private static EstornoLancamento CreateEstorno(EstornoLancamentoStatus status)
    {
        var estorno = new EstornoLancamento(
            Guid.NewGuid(),
            "m1",
            "Erro operacional no lancamento original",
            Guid.NewGuid());

        typeof(EstornoLancamento)
            .GetProperty(nameof(EstornoLancamento.Status), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(estorno, status);

        return estorno;
    }
}
