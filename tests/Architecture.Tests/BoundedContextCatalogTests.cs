using System.Xml.Linq;

namespace Architecture.Tests;

public sealed class BoundedContextCatalogTests
{
    [Fact]
    public void Catalog_should_include_all_current_bounded_contexts()
    {
        string[] serviceNames = [.. BoundedContextCatalog.Contexts
            .Select(context => context.ServiceName)
            .Order(StringComparer.Ordinal)];

        Assert.Equal(
            ["AuditService", "BalanceService", "IdentityService", "LedgerService", "PaymentService", "TransferService"],
            serviceNames);
    }

    [Fact]
    public void Catalog_governance_should_detect_unknown_context_folder()
    {
        using TempRepository tempRepository = TempRepository.Create();
        tempRepository.CreateServiceProject("unknown", "UnknownService", ArchitectureLayer.Domain);

        IReadOnlyList<string> uncataloged = CatalogGovernance.FindUncatalogedContextFolders(
            tempRepository.Root,
            BoundedContextCatalog.Contexts);

        Assert.Equal(["unknown"], uncataloged);
    }

    [Fact]
    public void Catalog_should_support_context_without_worker()
    {
        BoundedContextDescriptor identity = BoundedContextCatalog.Get("IdentityService");

        Assert.False(identity.HasWorker);
        Assert.DoesNotContain(ArchitectureLayer.Worker, identity.Layers);
    }

    [Fact]
    public void Catalog_should_support_context_with_worker()
    {
        BoundedContextDescriptor audit = BoundedContextCatalog.Get("AuditService");

        Assert.True(audit.HasWorker);
        Assert.Contains(ArchitectureLayer.Worker, audit.Layers);
    }

    [Fact]
    public void Catalog_governance_should_report_forbidden_internal_dependency()
    {
        using TempRepository tempRepository = TempRepository.Create();
        tempRepository.CreateServiceProject("bad", "BadService", ArchitectureLayer.Application, ["BadService.Infrastructure.csproj"]);

        BoundedContextDescriptor context = new(
            "BadService",
            "bad",
            new HashSet<ArchitectureLayer> { ArchitectureLayer.Domain, ArchitectureLayer.Application, ArchitectureLayer.Infrastructure },
            HasPersistence: false,
            new HashSet<MessagingProvider>(),
            new Dictionary<ArchitectureLayer, IReadOnlySet<ArchitectureLayer>>
            {
                [ArchitectureLayer.Application] = new HashSet<ArchitectureLayer> { ArchitectureLayer.Domain }
            },
            new Dictionary<ArchitectureLayer, IReadOnlySet<string>>(),
            new HashSet<string>());

        ProjectModel project = new(tempRepository.ProjectPath("bad", "BadService", ArchitectureLayer.Application));

        IReadOnlyList<string> violations = CatalogGovernance.FindForbiddenInternalProjectReferences(
            context,
            ArchitectureLayer.Application,
            project);

        Assert.Equal(["BadService.Infrastructure.csproj"], violations);
    }

    [Fact]
    public void Catalog_governance_should_accept_allowed_internal_dependency()
    {
        using TempRepository tempRepository = TempRepository.Create();
        tempRepository.CreateServiceProject("ok", "OkService", ArchitectureLayer.Application, ["OkService.Domain.csproj"]);

        BoundedContextDescriptor context = new(
            "OkService",
            "ok",
            new HashSet<ArchitectureLayer> { ArchitectureLayer.Domain, ArchitectureLayer.Application },
            HasPersistence: false,
            new HashSet<MessagingProvider>(),
            new Dictionary<ArchitectureLayer, IReadOnlySet<ArchitectureLayer>>
            {
                [ArchitectureLayer.Application] = new HashSet<ArchitectureLayer> { ArchitectureLayer.Domain }
            },
            new Dictionary<ArchitectureLayer, IReadOnlySet<string>>(),
            new HashSet<string>());

        ProjectModel project = new(tempRepository.ProjectPath("ok", "OkService", ArchitectureLayer.Application));

        IReadOnlyList<string> violations = CatalogGovernance.FindForbiddenInternalProjectReferences(
            context,
            ArchitectureLayer.Application,
            project);

        Assert.Empty(violations);
    }

    [Fact]
    public void Catalog_should_express_provider_specific_policy()
    {
        BoundedContextDescriptor audit = BoundedContextCatalog.Get("AuditService");
        BoundedContextDescriptor identity = BoundedContextCatalog.Get("IdentityService");
        BoundedContextDescriptor ledger = BoundedContextCatalog.Get("LedgerService");

        Assert.Contains(MessagingProvider.Kafka, audit.AllowedMessagingProviders);
        Assert.DoesNotContain(MessagingProvider.PubSub, audit.AllowedMessagingProviders);
        Assert.Empty(identity.AllowedMessagingProviders);
        Assert.Contains(MessagingProvider.PubSub, ledger.AllowedMessagingProviders);
    }

    [Fact]
    public void Lexical_policy_should_ignore_comments_and_strings()
    {
        const string source = """
            namespace Sample;

            // Kafka appears only in a comment.
            public sealed class Policy
            {
                private const string Text = "PubSub and Stripe appear only in a string";

                public void Run()
                {
                    AllowedSymbol();
                }
            }
            """;

        string codeOnly = CSharpLexicalText.RemoveCommentsAndStrings(source);

        Assert.DoesNotContain("Kafka", codeOnly, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PubSub", codeOnly, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stripe", codeOnly, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AllowedSymbol", codeOnly, StringComparison.Ordinal);
    }

    private sealed class TempRepository : IDisposable
    {
        private TempRepository(DirectoryInfo root)
        {
            Root = root;
            Directory.CreateDirectory(Path.Combine(root.FullName, "src"));
            File.WriteAllText(Path.Combine(root.FullName, "PocArquitetura.slnx"), "<Solution />");
        }

        public DirectoryInfo Root
        {
            get;
        }

        public static TempRepository Create()
            => new(new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"architecture-tests-{Guid.NewGuid():N}")));

        public void CreateServiceProject(
            string folder,
            string serviceName,
            ArchitectureLayer layer,
            IReadOnlyList<string>? projectReferences = null)
        {
            string projectDirectory = Path.Combine(Root.FullName, "src", folder, $"{serviceName}.{layer}");
            Directory.CreateDirectory(projectDirectory);

            XElement itemGroup = new("ItemGroup",
                (projectReferences ?? []).Select(reference =>
                    new XElement("ProjectReference", new XAttribute("Include", $"..\\{Path.GetFileNameWithoutExtension(reference)}\\{reference}"))));

            XDocument document = new(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    projectReferences is { Count: > 0 } ? itemGroup : null));

            document.Save(ProjectPath(folder, serviceName, layer));
        }

        public string ProjectPath(string folder, string serviceName, ArchitectureLayer layer)
            => Path.Combine(Root.FullName, "src", folder, $"{serviceName}.{layer}", $"{serviceName}.{layer}.csproj");

        public void Dispose()
        {
            if (Root.Exists)
            {
                Root.Delete(recursive: true);
            }
        }
    }
}
