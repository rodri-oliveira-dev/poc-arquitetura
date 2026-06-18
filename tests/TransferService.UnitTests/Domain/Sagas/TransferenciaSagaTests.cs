using TransferService.Domain.Exceptions;
using TransferService.Domain.Sagas;

namespace TransferService.UnitTests.Domain.Sagas;

public sealed class TransferenciaSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_should_create_valid_saga()
    {
        var saga = CreateSaga();

        Assert.Equal(new MerchantId("source"), saga.SourceMerchantId);
        Assert.Equal(new MerchantId("destination"), saga.DestinationMerchantId);
        Assert.Equal(new TransferAmount(100m), saga.Amount);
        Assert.Equal(TransferenciaSagaStatus.Pending, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Created, saga.Step);
        Assert.False(saga.DebitCreated);
        Assert.False(saga.CreditCreated);
        Assert.Equal(Now, saga.CreatedAt);
        Assert.Equal(Now, saga.UpdatedAt);
    }

    [Fact]
    public void Constructor_should_reject_same_merchants()
    {
        var act = () => new TransferenciaSaga(
            new MerchantId("same"),
            new MerchantId("same"),
            new TransferAmount(100m),
            Now);

        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("nao pode ser igual", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MerchantId_should_reject_empty_value(string value)
    {
        void Act() => _ = new MerchantId(value);

        var exception = Assert.Throws<DomainException>(Act);
        Assert.Contains("obrigatorio", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amount_should_reject_non_positive_value(decimal value)
    {
        void Act() => _ = new TransferAmount(value);

        var exception = Assert.Throws<DomainException>(Act);
        Assert.Contains("maior que zero", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Methods_should_apply_valid_transitions_until_completed()
    {
        var saga = CreateSaga();

        saga.StartProcessing(Now.AddMinutes(1));
        Assert.Equal(TransferenciaSagaStatus.Processing, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Processing, saga.Step);

        saga.MarkDebitCreating(Now.AddMinutes(2));
        Assert.Equal(TransferenciaSagaStatus.DebitCreating, saga.Status);
        Assert.Equal(TransferenciaSagaStep.DebitCreation, saga.Step);

        saga.MarkDebitCreated(Now.AddMinutes(3));
        Assert.Equal(TransferenciaSagaStatus.DebitCreated, saga.Status);
        Assert.True(saga.DebitCreated);

        saga.MarkCreditCreating(Now.AddMinutes(4));
        Assert.Equal(TransferenciaSagaStatus.CreditCreating, saga.Status);
        Assert.False(saga.CreditCreated);

        saga.MarkCompleted(Now.AddMinutes(5));
        Assert.Equal(TransferenciaSagaStatus.Completed, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Completed, saga.Step);
        Assert.True(saga.CreditCreated);
        Assert.Equal(Now.AddMinutes(5), saga.UpdatedAt);
    }

    [Fact]
    public void Methods_should_reject_invalid_transition_order()
    {
        var saga = CreateSaga();

        var exception = Assert.Throws<DomainException>(() => saga.MarkDebitCreating(Now.AddMinutes(1)));

        Assert.Contains("inicio do processamento", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TransferenciaSagaStatus.Pending, saga.Status);
    }

    [Fact]
    public void MarkCompleted_should_reject_when_debit_was_not_created()
    {
        var saga = CreateSaga();
        saga.StartProcessing(Now.AddMinutes(1));
        saga.MarkDebitCreating(Now.AddMinutes(2));

        var exception = Assert.Throws<DomainException>(() => saga.MarkCompleted(Now.AddMinutes(3)));

        Assert.Contains("criacao do credito", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(saga.CreditCreated);
        Assert.Equal(TransferenciaSagaStatus.DebitCreating, saga.Status);
    }

    [Fact]
    public void MarkCompleted_should_reject_when_credit_was_not_started()
    {
        var saga = CreateSagaWithDebitCreated();

        var exception = Assert.Throws<DomainException>(() => saga.MarkCompleted(Now.AddMinutes(4)));

        Assert.Contains("criacao do credito", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(saga.CreditCreated);
        Assert.Equal(TransferenciaSagaStatus.DebitCreated, saga.Status);
    }

    [Fact]
    public void MarkCompensationRequested_should_reject_before_debit_created()
    {
        var saga = CreateSaga();
        saga.StartProcessing(Now.AddMinutes(1));

        var exception = Assert.Throws<DomainException>(() => saga.MarkCompensationRequested(Now.AddMinutes(2)));

        Assert.Contains("apos debito criado", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TransferenciaSagaStatus.Processing, saga.Status);
    }

    [Fact]
    public void Methods_should_apply_valid_compensation_transitions()
    {
        var saga = CreateSagaWithDebitCreated();

        saga.MarkCompensationRequested(Now.AddMinutes(4));
        Assert.Equal(TransferenciaSagaStatus.CompensationRequested, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Compensation, saga.Step);

        saga.MarkCompensated(Now.AddMinutes(5));
        Assert.Equal(TransferenciaSagaStatus.Compensated, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Compensated, saga.Step);
    }

    [Fact]
    public void Methods_should_reject_changes_after_final_status()
    {
        var saga = CreateSagaWithDebitCreated();
        saga.MarkCreditCreating(Now.AddMinutes(4));
        saga.MarkCompleted(Now.AddMinutes(5));

        var exception = Assert.Throws<DomainException>(() => saga.MarkFailed(Now.AddMinutes(6)));

        Assert.Contains("finalizada", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TransferenciaSagaStatus.Completed, saga.Status);
    }

    [Fact]
    public void MarkRejected_should_reject_saga_with_created_debit()
    {
        var saga = CreateSagaWithDebitCreated();

        var exception = Assert.Throws<DomainException>(() => saga.MarkRejected(Now.AddMinutes(4)));

        Assert.Contains("compensada", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TransferenciaSagaStatus.DebitCreated, saga.Status);
    }

    [Fact]
    public void MarkRejected_should_finalize_saga_before_debit_is_created()
    {
        var saga = CreateSaga();
        saga.StartProcessing(Now.AddMinutes(1));

        saga.MarkRejected(Now.AddMinutes(2));

        Assert.Equal(TransferenciaSagaStatus.Rejected, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Rejected, saga.Step);
    }

    [Fact]
    public void MarkFailed_should_finalize_non_final_saga()
    {
        var saga = CreateSaga();
        saga.StartProcessing(Now.AddMinutes(1));

        saga.MarkFailed(Now.AddMinutes(2));

        Assert.Equal(TransferenciaSagaStatus.Failed, saga.Status);
        Assert.Equal(TransferenciaSagaStep.Failed, saga.Step);
    }

    private static TransferenciaSaga CreateSaga() =>
        new(
            new MerchantId("source"),
            new MerchantId("destination"),
            new TransferAmount(100m),
            Now);

    private static TransferenciaSaga CreateSagaWithDebitCreated()
    {
        var saga = CreateSaga();
        saga.StartProcessing(Now.AddMinutes(1));
        saga.MarkDebitCreating(Now.AddMinutes(2));
        saga.MarkDebitCreated(Now.AddMinutes(3));
        return saga;
    }
}
