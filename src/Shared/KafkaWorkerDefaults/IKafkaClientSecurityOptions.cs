namespace PocArquitetura.KafkaWorkerDefaults;

public interface IKafkaClientSecurityOptions
{
    string SecurityProtocol
    {
        get;
    }
    string SaslMechanism
    {
        get;
    }
    string SaslUsername
    {
        get;
    }
    string SaslPassword
    {
        get;
    }
    string SslCaLocation
    {
        get;
    }
}
