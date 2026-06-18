namespace TransferService.Infrastructure.Persistence.Outbox;

public enum TransferenciaOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Published = 2,
    DeadLetter = 3
}
