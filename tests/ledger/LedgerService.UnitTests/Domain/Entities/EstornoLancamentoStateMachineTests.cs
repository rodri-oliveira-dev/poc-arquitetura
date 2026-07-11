using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;

namespace LedgerService.UnitTests.Domain.Entities;

public sealed class EstornoLancamentoStateMachineTests
{
    [Fact]
    public void Should_create_pending_estorno()
    {
        var estorno = NewEstorno();

        Assert.Equal(EstornoLancamentoStatus.Pending, estorno.Status);
        Assert.True(estorno.IsActive());
        Assert.False(estorno.IsFinal());
    }

    [Fact]
    public void Should_complete_after_processing()
    {
        var estorno = NewEstorno();
        var now = DateTime.UtcNow;
        var compensatingId = Guid.NewGuid();

        estorno.MarkProcessing(now);
        estorno.Complete(compensatingId, now.AddSeconds(1));

        Assert.Equal(EstornoLancamentoStatus.Completed, estorno.Status);
        Assert.Equal(compensatingId, estorno.LancamentoCompensatorioId);
        Assert.Equal(now, estorno.ProcessingStartedAt);
        Assert.Equal(now.AddSeconds(1), estorno.CompletedAt);
    }

    [Fact]
    public void Should_reject_from_pending()
    {
        var estorno = NewEstorno();
        var now = DateTime.UtcNow;

        estorno.Reject("Lancamento inexistente", now);

        Assert.Equal(EstornoLancamentoStatus.Rejected, estorno.Status);
        Assert.Equal(now, estorno.RejectedAt);
        Assert.Equal("Lancamento inexistente", estorno.RejectionReason);
    }

    [Fact]
    public void Should_fail_from_processing()
    {
        var estorno = NewEstorno();
        var now = DateTime.UtcNow;

        estorno.MarkProcessing(now);
        estorno.Fail("Falha tecnica", now.AddSeconds(1));

        Assert.Equal(EstornoLancamentoStatus.Failed, estorno.Status);
        Assert.Equal(now.AddSeconds(1), estorno.FailedAt);
        Assert.Equal("Falha tecnica", estorno.FailureReason);
    }

    [Theory]
    [InlineData(EstornoLancamentoStatus.Completed)]
    [InlineData(EstornoLancamentoStatus.Rejected)]
    [InlineData(EstornoLancamentoStatus.Failed)]
    public void Should_not_leave_final_state(EstornoLancamentoStatus finalStatus)
    {
        var estorno = MoveTo(finalStatus);

        Assert.Throws<DomainException>(() => estorno.MarkProcessing(DateTime.UtcNow));
        Assert.Throws<DomainException>(() => estorno.Complete(Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<DomainException>(() => estorno.Reject("rejeitar", DateTime.UtcNow));
        Assert.Throws<DomainException>(() => estorno.Fail("falhar", DateTime.UtcNow));
    }

    [Fact]
    public void Should_not_complete_without_processing()
    {
        var estorno = NewEstorno();

        var ex = Assert.Throws<DomainException>(() => estorno.Complete(Guid.NewGuid(), DateTime.UtcNow));

        Assert.Contains("Transicao", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EstornoLancamento MoveTo(EstornoLancamentoStatus status)
    {
        var estorno = NewEstorno();
        var now = DateTime.UtcNow;

        switch (status)
        {
            case EstornoLancamentoStatus.Completed:
                estorno.MarkProcessing(now);
                estorno.Complete(Guid.NewGuid(), now);
                break;
            case EstornoLancamentoStatus.Rejected:
                estorno.Reject("rejeitado", now);
                break;
            case EstornoLancamentoStatus.Failed:
                estorno.Fail("falhou", now);
                break;
            case EstornoLancamentoStatus.Pending:
                break;
            case EstornoLancamentoStatus.Processing:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Estado nao final.");
        }

        return estorno;
    }

    private static EstornoLancamento NewEstorno()
        => new(Guid.NewGuid(), "m1", "Erro operacional", Guid.NewGuid(), DateTime.UtcNow);
}
