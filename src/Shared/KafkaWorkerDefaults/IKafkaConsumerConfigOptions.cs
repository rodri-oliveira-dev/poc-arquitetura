namespace PocArquitetura.KafkaWorkerDefaults;

public interface IKafkaConsumerConfigOptions : IKafkaClientSecurityOptions
{
    string BootstrapServers
    {
        get;
    }
    string GroupId
    {
        get;
    }
    string ClientId
    {
        get;
    }
    bool EnableAutoCommit
    {
        get;
    }
    bool EnableAutoOffsetStore
    {
        get;
    }
    bool AllowAutoCreateTopics
    {
        get;
    }
    string AutoOffsetReset
    {
        get;
    }
}
