using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Payments;

namespace PaymentService.UnitTests.Domain.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_should_initialize_pending_payment()
    {
        var payment = CreatePayment();

        Assert.NotEqual(Guid.Empty, payment.PaymentId.Value);
        Assert.Equal(new MerchantId("merchant-001"), payment.MerchantId);
        Assert.Equal(100.25m, payment.Amount.Amount);
        Assert.Equal(Currency.Brl, payment.Amount.Currency);
        Assert.Equal(PaymentProvider.Stripe, payment.Provider);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(new ExternalReference("order-123"), payment.ExternalReference);
        Assert.Equal(Now, payment.CreatedAt);
        Assert.Equal(Now, payment.UpdatedAt);
        Assert.Null(payment.ExternalPaymentReference);
        Assert.Null(payment.LedgerEntryReference);
        Assert.Equal(LedgerIntegrationStatus.NotRequired, payment.LedgerIntegrationStatus);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Money_should_reject_non_positive_amount(decimal amount)
    {
        var exception = Assert.Throws<DomainException>(() => new Money(amount, Currency.Brl));
        Assert.Contains("maior que zero", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Currency_should_allow_only_brl(string currency)
    {
        Assert.Throws<DomainException>(() => new Currency(currency));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MerchantId_should_reject_empty_value(string merchantId)
    {
        Assert.Throws<DomainException>(() => new MerchantId(merchantId));
    }

    [Fact]
    public void ExternalReference_should_reject_values_above_limit()
    {
        Assert.Throws<DomainException>(() => new ExternalReference(new string('x', ExternalReference.MaxLength + 1)));
    }

    [Fact]
    public void Constructor_should_reject_invalid_provider()
    {
        Assert.Throws<DomainException>(() => new Payment(
            PaymentId.New(),
            new MerchantId("merchant-001"),
            new Money(10m, Currency.Brl),
            (PaymentProvider)999,
            Now));
    }

    [Fact]
    public void State_machine_should_allow_documented_provider_transitions()
    {
        var requiresAction = CreatePayment();
        Assert.True(requiresAction.MarkRequiresAction(Now.AddMinutes(1)));
        Assert.Equal(PaymentStatus.RequiresAction, requiresAction.Status);
        Assert.True(requiresAction.MarkProcessing(Now.AddMinutes(2)));
        Assert.Equal(PaymentStatus.Processing, requiresAction.Status);
        Assert.True(requiresAction.MarkSucceeded(Now.AddMinutes(3), new ExternalPaymentReference("pi_123"), "succeeded"));
        Assert.Equal(PaymentStatus.Succeeded, requiresAction.Status);

        var successDirect = CreatePayment();
        Assert.True(successDirect.MarkSucceeded(Now.AddMinutes(1)));
        Assert.Equal(PaymentStatus.Succeeded, successDirect.Status);

        var failed = CreatePayment();
        Assert.True(failed.MarkFailed(Now.AddMinutes(1), "payment_failed"));
        Assert.Equal(PaymentStatus.Failed, failed.Status);

        var cancelled = CreatePayment();
        Assert.True(cancelled.Cancel(Now.AddMinutes(1), "cancelled"));
        Assert.Equal(PaymentStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void State_machine_should_distinguish_succeeded_ledger_pending_and_completed()
    {
        var payment = CreatePayment();

        payment.MarkSucceeded(Now.AddMinutes(1));
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(LedgerIntegrationStatus.Pending, payment.LedgerIntegrationStatus);
        Assert.Null(payment.LedgerEntryReference);

        payment.MarkLedgerEntryRequested(Now.AddMinutes(2));
        Assert.Equal(PaymentStatus.LedgerPending, payment.Status);
        Assert.Equal(LedgerIntegrationStatus.Processing, payment.LedgerIntegrationStatus);
        Assert.Null(payment.LedgerEntryReference);

        payment.MarkCompleted(Now.AddMinutes(3), new LedgerEntryReference(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal(LedgerIntegrationStatus.Completed, payment.LedgerIntegrationStatus);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), payment.LedgerEntryReference?.Value);
        Assert.Equal(Now.AddMinutes(3), payment.CompletedAt);
    }

    [Fact]
    public void Repeated_same_transition_should_be_idempotent()
    {
        var payment = CreatePayment();

        Assert.True(payment.MarkProcessing(Now.AddMinutes(1)));
        Assert.False(payment.MarkProcessing(Now.AddMinutes(2)));
        Assert.Equal(PaymentStatus.Processing, payment.Status);
    }

    [Fact]
    public void Regression_after_succeeded_should_be_ignored()
    {
        var payment = CreatePayment();

        payment.MarkSucceeded(Now.AddMinutes(1));
        var changed = payment.MarkProcessing(Now.AddMinutes(2));

        Assert.False(changed);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void Terminal_state_should_reject_incompatible_transitions()
    {
        var payment = CreatePayment();
        payment.MarkFailed(Now.AddMinutes(1));

        var exception = Assert.Throws<DomainException>(() => payment.MarkSucceeded(Now.AddMinutes(2)));
        Assert.Contains("estado final", exception.Message);
    }

    [Fact]
    public void Completed_payment_should_not_regress_to_failed_or_cancelled()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded(Now.AddMinutes(1));
        payment.MarkCompleted(Now.AddMinutes(2), new LedgerEntryReference(Guid.NewGuid()));

        Assert.Throws<DomainException>(() => payment.MarkFailed(Now.AddMinutes(3)));
        Assert.Throws<DomainException>(() => payment.Cancel(Now.AddMinutes(3)));
    }

    private static Payment CreatePayment()
        => new(
            PaymentId.New(),
            new MerchantId("merchant-001"),
            new Money(100.25m, Currency.Brl),
            PaymentProvider.Stripe,
            Now,
            "Pagamento de pedido",
            new ExternalReference("order-123"));
}
