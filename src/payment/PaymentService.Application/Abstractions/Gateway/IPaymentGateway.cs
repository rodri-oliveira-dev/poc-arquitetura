namespace PaymentService.Application.Abstractions.Gateway;

public interface IPaymentGateway
{
    Task<CreateExternalPaymentResult> CreatePaymentIntentAsync(
        CreateExternalPaymentRequest request,
        CancellationToken cancellationToken);
}
