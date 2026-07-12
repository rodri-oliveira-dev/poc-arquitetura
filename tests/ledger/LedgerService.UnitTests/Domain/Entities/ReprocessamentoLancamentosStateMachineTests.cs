using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;

namespace LedgerService.UnitTests.Domain.Entities;

public sealed class ReprocessamentoLancamentosStateMachineTests
{
    [Fact]
    public void Should_reject_invalid_period()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new ReprocessamentoLancamentos(
                "m1",
                new DateOnly(2026, 5, 6),
                new DateOnly(2026, 5, 1),
                "Correcao",
                Guid.NewGuid(),
                DateTime.UtcNow));

        Assert.Contains("DataFinal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_complete_with_records()
    {
        var reprocessamento = NewReprocessamento();
        var now = DateTime.UtcNow;

        reprocessamento.MarkProcessing(now);
        reprocessamento.Complete(3, now.AddSeconds(1));

        Assert.Equal(ReprocessamentoLancamentosStatus.Completed, reprocessamento.Status);
        Assert.Equal(now.AddSeconds(1), reprocessamento.CompletedAt);
        Assert.Null(reprocessamento.FailureReason);
    }

    [Fact]
    public void Should_complete_with_warnings_when_no_records()
    {
        var reprocessamento = NewReprocessamento();
        var now = DateTime.UtcNow;

        reprocessamento.MarkProcessing(now);
        reprocessamento.Complete(0, now.AddSeconds(1));

        Assert.Equal(ReprocessamentoLancamentosStatus.CompletedWithWarnings, reprocessamento.Status);
        Assert.Contains("Nenhum lancamento", reprocessamento.FailureReason);
    }

    [Fact]
    public void Should_cancel_from_pending()
    {
        var reprocessamento = NewReprocessamento();
        var now = DateTime.UtcNow;

        reprocessamento.Cancel("Cancelado pelo operador", now);

        Assert.Equal(ReprocessamentoLancamentosStatus.Canceled, reprocessamento.Status);
        Assert.Equal(now, reprocessamento.RejectedAt);
        Assert.Equal("Cancelado pelo operador", reprocessamento.RejectionReason);
    }

    [Theory]
    [InlineData(ReprocessamentoLancamentosStatus.Completed)]
    [InlineData(ReprocessamentoLancamentosStatus.CompletedWithWarnings)]
    [InlineData(ReprocessamentoLancamentosStatus.Failed)]
    [InlineData(ReprocessamentoLancamentosStatus.Rejected)]
    [InlineData(ReprocessamentoLancamentosStatus.Canceled)]
    public void Should_not_leave_final_state(ReprocessamentoLancamentosStatus finalStatus)
    {
        var reprocessamento = MoveTo(finalStatus);

        Assert.Throws<DomainException>(() => reprocessamento.MarkProcessing(DateTime.UtcNow));
        Assert.Throws<DomainException>(() => reprocessamento.Complete(1, DateTime.UtcNow));
        Assert.Throws<DomainException>(() => reprocessamento.Fail("falha", DateTime.UtcNow));
        Assert.Throws<DomainException>(() => reprocessamento.Reject("rejeicao", DateTime.UtcNow));
        Assert.Throws<DomainException>(() => reprocessamento.Cancel("cancelamento", DateTime.UtcNow));
    }

    [Fact]
    public void Should_reject_negative_processed_count()
    {
        var reprocessamento = NewReprocessamento();
        reprocessamento.MarkProcessing(DateTime.UtcNow);

        Assert.Throws<DomainException>(() => reprocessamento.Complete(-1, DateTime.UtcNow));
    }

    private static ReprocessamentoLancamentos MoveTo(ReprocessamentoLancamentosStatus status)
    {
        var reprocessamento = NewReprocessamento();
        var now = DateTime.UtcNow;

        switch (status)
        {
            case ReprocessamentoLancamentosStatus.Completed:
                reprocessamento.MarkProcessing(now);
                reprocessamento.Complete(1, now);
                break;
            case ReprocessamentoLancamentosStatus.CompletedWithWarnings:
                reprocessamento.MarkProcessing(now);
                reprocessamento.Complete(0, now);
                break;
            case ReprocessamentoLancamentosStatus.Failed:
                reprocessamento.Fail("falhou", now);
                break;
            case ReprocessamentoLancamentosStatus.Rejected:
                reprocessamento.Reject("rejeitado", now);
                break;
            case ReprocessamentoLancamentosStatus.Canceled:
                reprocessamento.Cancel("cancelado", now);
                break;
            case ReprocessamentoLancamentosStatus.Pending:
                break;
            case ReprocessamentoLancamentosStatus.Processing:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Estado nao final.");
        }

        return reprocessamento;
    }

    private static ReprocessamentoLancamentos NewReprocessamento()
        => new(
            "m1",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 6),
            "Correcao de regra",
            Guid.NewGuid(),
            DateTime.UtcNow);
}
