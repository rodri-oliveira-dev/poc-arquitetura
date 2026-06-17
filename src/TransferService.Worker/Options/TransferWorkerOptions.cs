namespace TransferService.Worker.Options;

public sealed class TransferWorkerOptions
{
    public const string SectionName = "TransferService:Worker";

    public bool Enabled { get; set; } = true;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 20;
    public int MaxRetryCount { get; set; } = 5;
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromSeconds(30);
    public TransferKafkaOptions Kafka { get; set; } = new();
    public TransferKafkaTopicsOptions Topics { get; set; } = new();
    public string DlqTopic { get; set; } = "transfer.transferencia.dlq";
    public LedgerClientOptions Ledger { get; set; } = new();
}

public sealed class TransferKafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string ClientId { get; set; } = "transfer-service-worker";
    public string SecurityProtocol { get; set; } = "Plaintext";
    public string Acks { get; set; } = "all";
    public bool EnableIdempotence { get; set; } = true;
    public int MessageTimeoutMs { get; set; } = 30000;
}

public sealed class TransferKafkaTopicsOptions
{
    public string Solicitada { get; set; } = "transfer.transferencia.solicitada";
    public string DebitoCriado { get; set; } = "transfer.transferencia.debito-criado";
    public string CreditoCriado { get; set; } = "transfer.transferencia.credito-criado";
    public string Concluida { get; set; } = "transfer.transferencia.concluida";
    public string CompensacaoSolicitada { get; set; } = "transfer.transferencia.compensacao-solicitada";
    public string Compensada { get; set; } = "transfer.transferencia.compensada";
    public string Falhou { get; set; } = "transfer.transferencia.falhou";
}

public sealed class LedgerClientOptions
{
    public Uri? BaseAddress { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
