namespace Architecture.Tests;

internal static class BoundedContextCatalog
{
    public static readonly IReadOnlyList<BoundedContextDescriptor> Contexts =
    [
        Context(
            "LedgerService",
            "ledger",
            hasWorker: true,
            hasPersistence: true,
            allowedMessagingProviders: new HashSet<MessagingProvider> { MessagingProvider.Kafka, MessagingProvider.PubSub },
            domainForbiddenTypeNameTerms: new HashSet<string> { "Kafka", "PubSub", "Persistence", "Serialization" }),

        Context(
            "BalanceService",
            "balance",
            hasWorker: true,
            hasPersistence: true,
            allowedMessagingProviders: new HashSet<MessagingProvider> { MessagingProvider.Kafka, MessagingProvider.PubSub },
            domainForbiddenTypeNameTerms: new HashSet<string>
            {
                "JsonPropertyName",
                "Kafka",
                "PubSub",
                "Inbox",
                "Outbox",
                "ProcessedEvent",
                "Consumer",
                "LedgerEntryCreatedEvent",
                "Message"
            }),

        Context(
            "TransferService",
            "transfer",
            hasWorker: true,
            hasPersistence: true,
            allowedMessagingProviders: new HashSet<MessagingProvider> { MessagingProvider.Kafka }),

        Context(
            "PaymentService",
            "payment",
            hasWorker: true,
            hasPersistence: true),

        Context(
            "IdentityService",
            "identity",
            hasWorker: false,
            hasPersistence: true),

        Context(
            "AuditService",
            "audit",
            hasWorker: true,
            hasPersistence: true,
            allowedMessagingProviders: new HashSet<MessagingProvider> { MessagingProvider.Kafka })
    ];

    public static BoundedContextDescriptor Get(string serviceName)
        => Contexts.Single(context => context.ServiceName == serviceName);

    private static BoundedContextDescriptor Context(
        string serviceName,
        string physicalFolder,
        bool hasWorker,
        bool hasPersistence,
        IReadOnlySet<MessagingProvider>? allowedMessagingProviders = null,
        IReadOnlySet<string>? domainForbiddenTypeNameTerms = null)
    {
        HashSet<ArchitectureLayer> layers =
        [
            ArchitectureLayer.Api,
            ArchitectureLayer.Application,
            ArchitectureLayer.Domain,
            ArchitectureLayer.Infrastructure
        ];

        if (hasWorker)
        {
            layers.Add(ArchitectureLayer.Worker);
        }

        return new BoundedContextDescriptor(
            serviceName,
            physicalFolder,
            layers,
            hasPersistence,
            allowedMessagingProviders ?? new HashSet<MessagingProvider>(),
            DefaultInternalReferences(hasWorker),
            DefaultSharedReferences(serviceName, hasWorker),
            domainForbiddenTypeNameTerms ?? new HashSet<string>());
    }

    private static Dictionary<ArchitectureLayer, IReadOnlySet<ArchitectureLayer>> DefaultInternalReferences(bool hasWorker)
    {
        Dictionary<ArchitectureLayer, IReadOnlySet<ArchitectureLayer>> references = new()
        {
            [ArchitectureLayer.Domain] = new HashSet<ArchitectureLayer>(),
            [ArchitectureLayer.Application] = new HashSet<ArchitectureLayer> { ArchitectureLayer.Domain },
            [ArchitectureLayer.Infrastructure] = new HashSet<ArchitectureLayer>
            {
                ArchitectureLayer.Application,
                ArchitectureLayer.Domain
            },
            [ArchitectureLayer.Api] = new HashSet<ArchitectureLayer>
            {
                ArchitectureLayer.Application,
                ArchitectureLayer.Infrastructure
            }
        };

        if (hasWorker)
        {
            references[ArchitectureLayer.Worker] = new HashSet<ArchitectureLayer>
            {
                ArchitectureLayer.Application,
                ArchitectureLayer.Infrastructure
            };
        }

        return references;
    }

    private static Dictionary<ArchitectureLayer, IReadOnlySet<string>> DefaultSharedReferences(
        string serviceName,
        bool hasWorker)
    {
        Dictionary<ArchitectureLayer, IReadOnlySet<string>> references = new()
        {
            [ArchitectureLayer.Api] = new HashSet<string> { "ApiDefaults.csproj" }
        };

        if (hasWorker && serviceName is "LedgerService" or "BalanceService" or "AuditService")
        {
            references[ArchitectureLayer.Worker] = new HashSet<string> { "KafkaWorkerDefaults.csproj" };
        }

        return references;
    }
}
