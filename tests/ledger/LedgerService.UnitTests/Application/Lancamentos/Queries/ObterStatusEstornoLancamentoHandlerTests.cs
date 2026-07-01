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
        Assert.Equal(estorno.Id, result.EstornoId);
        Assert.Equal(estorno.LancamentoOriginalId, result.LancamentoOriginalId);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(estorno.Motivo, result.Motivo);
        Assert.Equal(estorno.CreatedAt, result.SolicitadoEm);
        Assert.NotEqual(estorno.GetType(), result.GetType());
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
        Assert.Equal(expected, result.Status);
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

        var ex = await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Contains("estorno", ex.Message);
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

        var ex = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Contains("merchant", ex.Message);
    }

    private static EstornoLancamento CreateEstorno(EstornoLancamentoStatus status)
    {
        var now = DateTime.UtcNow;
        var estorno = new EstornoLancamento(
            Guid.NewGuid(),
            "m1",
            "Erro operacional no lancamento original",
            Guid.NewGuid(),
            now);

        switch (status)
        {
            case EstornoLancamentoStatus.Pending:
                break;
            case EstornoLancamentoStatus.Processing:
                estorno.MarkProcessing(now);
                break;
            case EstornoLancamentoStatus.Completed:
                estorno.MarkProcessing(now);
                estorno.Complete(Guid.NewGuid(), now);
                break;
            case EstornoLancamentoStatus.Failed:
                estorno.Fail("Falha tecnica ao processar estorno.", now);
                break;
            case EstornoLancamentoStatus.Rejected:
                estorno.Reject("Lancamento original nao encontrado.", now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Status de estorno nao suportado pelo teste.");
        }

        return estorno;
    }
}
