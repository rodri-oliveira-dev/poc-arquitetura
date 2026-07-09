using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Payments.InboxProcessing;

public interface IProviderEventMapper
{
    ProviderEventMappingResult Map(PaymentInboxMessage inboxMessage);
}
