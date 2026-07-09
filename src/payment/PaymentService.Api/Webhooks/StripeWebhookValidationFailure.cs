namespace PaymentService.Api.Webhooks;

public enum StripeWebhookValidationFailure
{
    MissingSignatureHeader = 1,
    MissingSecret = 2,
    MalformedSignatureHeader = 3,
    TimestampOutsideTolerance = 4,
    InvalidSignature = 5,
    InvalidPayload = 6
}
