using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ArchArchitecture = ArchUnitNET.Domain.Architecture;
using ReflectionAssembly = System.Reflection.Assembly;
using ReflectionType = System.Type;

namespace Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly string[] _domainForbiddenReferences =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Confluent.Kafka",
        "Google.Cloud.PubSub.V1",
        "Stripe"
    ];

    private static readonly string[] _applicationForbiddenReferences =
    [
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.OpenApi",
        "Swashbuckle.AspNetCore",
        "Confluent.Kafka",
        "Google.Cloud.PubSub.V1"
    ];

    private static readonly string[] _workerForbiddenReferences =
    [
        "Microsoft.OpenApi",
        "Swashbuckle.AspNetCore"
    ];

    private static readonly string[] _workerHttpPresentationTerms =
    [
        "ControllerBase",
        "MapControllers",
        "Swagger"
    ];

    private static readonly string[] _apiWorkerTerms =
    [
        "AddHostedService",
        "BackgroundService"
    ];

    private static readonly ReflectionAssembly[] _productionAssemblies =
    [
        .. BoundedContextCatalog.Contexts.SelectMany(context =>
            context.Layers.Select(layer => LoadAssembly(context.AssemblyName(layer))))
    ];

    private static readonly ArchArchitecture _architecture = new ArchLoader()
        .LoadAssemblies(_productionAssemblies)
        .Build();

    [Fact]
    public void Source_context_folders_should_be_cataloged()
    {
        IReadOnlyList<string> uncataloged = CatalogGovernance.FindUncatalogedContextFolders(
            ArchitectureTestPaths.RepositoryRoot,
            BoundedContextCatalog.Contexts);

        Assert.True(
            uncataloged.Count == 0,
            "Every bounded context folder under src must be represented in BoundedContextCatalog. "
            + $"Uncataloged folders: {string.Join(", ", uncataloged)}");
    }

    [Theory]
    [MemberData(nameof(ContextLayers), ArchitectureLayer.Domain)]
    public void Domain_should_not_depend_on_web_ef_core_or_providers(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        AssertNoForbiddenDependencies(context.AssemblyName(ArchitectureLayer.Domain), _domainForbiddenReferences);
        AssertProjectHasNoForbiddenReferences(context, ArchitectureLayer.Domain, _domainForbiddenReferences);
        AssertProjectReferencesOnlyAllowedLayers(context, ArchitectureLayer.Domain);
        AssertDomainDoesNotDefineForbiddenTechnicalTypes(context);
    }

    [Theory]
    [MemberData(nameof(ContextLayers), ArchitectureLayer.Application)]
    public void Application_should_not_depend_on_http_swagger_ef_core_or_provider_adapters(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        AssertNoForbiddenDependencies(context.AssemblyName(ArchitectureLayer.Application), _applicationForbiddenReferences);
        AssertProjectHasNoForbiddenReferences(context, ArchitectureLayer.Application, _applicationForbiddenReferences);
        AssertProjectReferencesOnlyAllowedLayers(context, ArchitectureLayer.Application);
    }

    [Theory]
    [MemberData(nameof(ContextLayers), ArchitectureLayer.Api)]
    public void Api_should_not_depend_on_worker_components(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        AssertProjectReferencesOnlyAllowedLayers(context, ArchitectureLayer.Api);
        AssertSourceFilesDoNotContainCodeTerms(context, ArchitectureLayer.Api, _apiWorkerTerms);

        Types().That().ResideInAssembly(context.AssemblyName(ArchitectureLayer.Api))
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace($"{context.ServiceName}.Worker"))
            .Because($"{context.ServiceName}.Api must not depend on Worker components")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Theory]
    [MemberData(nameof(ContextLayers), ArchitectureLayer.Api)]
    public void Api_should_not_depend_on_concrete_repositories(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        Types().That().ResideInAssembly(context.AssemblyName(ArchitectureLayer.Api))
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace($"{context.ServiceName}.Infrastructure.Persistence.Repositories"))
            .Because($"{context.ServiceName}.Api must call application use cases instead of concrete repositories")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Theory]
    [MemberData(nameof(ContextLayers), ArchitectureLayer.Worker)]
    public void Worker_should_not_depend_on_http_presentation(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        AssertProjectReferencesOnlyAllowedLayers(context, ArchitectureLayer.Worker);
        AssertProjectHasNoForbiddenReferences(context, ArchitectureLayer.Worker, _workerForbiddenReferences);
        AssertNoForbiddenDependencies(context.AssemblyName(ArchitectureLayer.Worker), _workerForbiddenReferences);
        AssertSourceFilesDoNotContainCodeTerms(context, ArchitectureLayer.Worker, _workerHttpPresentationTerms);

        Types().That().ResideInAssembly(context.AssemblyName(ArchitectureLayer.Worker))
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace($"{context.ServiceName}.Api.Controllers")
                    .Or().ResideInNamespace($"{context.ServiceName}.Api.Swagger"))
            .Because($"{context.ServiceName}.Worker must not depend on controllers or Swagger types")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Projects_should_reference_only_allowed_internal_layers(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        foreach (ArchitectureLayer layer in context.Layers)
        {
            AssertProjectReferencesOnlyAllowedLayers(context, layer);
        }
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Provider_packages_should_exist_only_where_catalog_allows_them(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        foreach (ArchitectureLayer layer in context.Layers)
        {
            AssertProviderPackagePolicy(context, layer);
        }
    }

    [Theory]
    [MemberData(nameof(PersistentContexts))]
    public void Persistent_contexts_should_keep_ef_core_in_infrastructure(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        ProjectModel infrastructure = LoadProject(context, ArchitectureLayer.Infrastructure);

        Assert.Contains(
            "Microsoft.EntityFrameworkCore",
            infrastructure.PackageAndFrameworkReferences);

        ReflectionType[] persistenceAdapterTypes = [.. LoadAssembly(context.AssemblyName(ArchitectureLayer.Infrastructure))
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => !type.IsNested)
            .Where(type => type.Namespace?.Contains(".Infrastructure.Persistence.", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal)
                || type.Name.EndsWith("QueryService", StringComparison.Ordinal)
                || type.Name.EndsWith("IdempotencyService", StringComparison.Ordinal))];

        Assert.NotEmpty(persistenceAdapterTypes);

        foreach (ReflectionType adapterType in persistenceAdapterTypes)
        {
            Assert.True(
                adapterType.GetInterfaces().Any(IsServicePort),
                $"{adapterType.FullName} must implement an application or domain port.");
        }
    }

    [Fact]
    public void PubSub_should_remain_only_in_legacy_ledger_and_balance_workers()
    {
        foreach (BoundedContextDescriptor context in BoundedContextCatalog.Contexts)
        {
            foreach (ArchitectureLayer layer in context.Layers)
            {
                bool pubSubAllowed = layer == ArchitectureLayer.Worker
                    && context.AllowedMessagingProviders.Contains(MessagingProvider.PubSub);

                if (pubSubAllowed)
                {
                    continue;
                }

                AssertProjectHasNoForbiddenReferences(context, layer, ["Google.Cloud.PubSub.V1"]);
                AssertSourceFilesDoNotContainCodeTerms(context, layer, ["PubSub", "Pub/Sub", "Google.Cloud.PubSub"]);
            }
        }
    }

    [Fact]
    public void TransferService_should_remain_kafka_only()
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get("TransferService");

        Assert.DoesNotContain(MessagingProvider.PubSub, context.AllowedMessagingProviders);

        foreach (ArchitectureLayer layer in context.Layers)
        {
            AssertProjectHasNoForbiddenReferences(context, layer, ["Google.Cloud.PubSub.V1"]);
            AssertSourceFilesDoNotContainCodeTerms(context, layer, ["PubSub", "Pub/Sub", "Google.Cloud.PubSub"]);
        }
    }

    [Fact]
    public void Stripe_concepts_should_remain_specific_to_payment()
    {
        foreach (BoundedContextDescriptor context in BoundedContextCatalog.Contexts.Where(context => context.ServiceName != "PaymentService"))
        {
            foreach (ArchitectureLayer layer in context.Layers)
            {
                AssertProjectHasNoForbiddenReferences(context, layer, ["Stripe"]);
                AssertSourceFilesDoNotContainCodeTerms(context, layer, ["Stripe"]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Contexts_should_not_reference_other_context_projects(string serviceName)
    {
        BoundedContextDescriptor context = BoundedContextCatalog.Get(serviceName);

        foreach (ArchitectureLayer layer in context.Layers)
        {
            ProjectModel project = LoadProject(context, layer);
            string[] violations = [.. project.ProjectReferenceFileNames
                .Where(reference => reference.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Where(reference => reference.Contains("Service.", StringComparison.Ordinal))
                .Where(reference => !reference.StartsWith($"{context.ServiceName}.", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)];

            Assert.True(
                violations.Length == 0,
                $"{context.ServiceName}.{layer}.csproj must not reference projects from another bounded context. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    public static TheoryData<string> Contexts()
    {
        TheoryData<string> data = [];

        foreach (BoundedContextDescriptor context in BoundedContextCatalog.Contexts)
        {
            data.Add(context.ServiceName);
        }

        return data;
    }

    public static TheoryData<string> PersistentContexts()
    {
        TheoryData<string> data = [];

        foreach (BoundedContextDescriptor context in BoundedContextCatalog.Contexts.Where(context => context.HasPersistence))
        {
            data.Add(context.ServiceName);
        }

        return data;
    }

    public static TheoryData<string> ContextLayers(ArchitectureLayer layer)
    {
        TheoryData<string> data = [];

        foreach (BoundedContextDescriptor context in BoundedContextCatalog.Contexts.Where(context => context.Layers.Contains(layer)))
        {
            data.Add(context.ServiceName);
        }

        return data;
    }

    private static void AssertProviderPackagePolicy(BoundedContextDescriptor context, ArchitectureLayer layer)
    {
        bool kafkaAllowed = layer == ArchitectureLayer.Worker
            && context.AllowedMessagingProviders.Contains(MessagingProvider.Kafka);
        bool pubSubAllowed = layer == ArchitectureLayer.Worker
            && context.AllowedMessagingProviders.Contains(MessagingProvider.PubSub);

        if (!kafkaAllowed)
        {
            AssertProjectHasNoForbiddenReferences(context, layer, ["Confluent.Kafka"]);
        }

        if (!pubSubAllowed)
        {
            AssertProjectHasNoForbiddenReferences(context, layer, ["Google.Cloud.PubSub.V1"]);
        }
    }

    private static void AssertProjectReferencesOnlyAllowedLayers(BoundedContextDescriptor context, ArchitectureLayer layer)
    {
        ProjectModel project = LoadProject(context, layer);
        IReadOnlyList<string> forbiddenInternalReferences =
            CatalogGovernance.FindForbiddenInternalProjectReferences(context, layer, project);
        IReadOnlyList<string> forbiddenSharedReferences =
            CatalogGovernance.FindForbiddenSharedProjectReferences(context, layer, project);

        Assert.True(
            forbiddenInternalReferences.Count == 0,
            $"{context.ServiceName}.{layer}.csproj references internal layers not allowed by BoundedContextCatalog. "
            + $"Found: {string.Join(", ", forbiddenInternalReferences)}");

        Assert.True(
            forbiddenSharedReferences.Count == 0,
            $"{context.ServiceName}.{layer}.csproj references shared projects not allowed by BoundedContextCatalog. "
            + $"Found: {string.Join(", ", forbiddenSharedReferences)}");
    }

    private static void AssertDomainDoesNotDefineForbiddenTechnicalTypes(BoundedContextDescriptor context)
    {
        if (context.DomainForbiddenTypeNameTerms.Count == 0)
        {
            return;
        }

        ReflectionType[] violations = [.. LoadAssembly(context.AssemblyName(ArchitectureLayer.Domain))
            .GetTypes()
            .Where(type => type.FullName is not null)
            .Where(type => context.DomainForbiddenTypeNameTerms.Any(term =>
                type.FullName!.Contains(term, StringComparison.OrdinalIgnoreCase)))];

        Assert.True(
            violations.Length == 0,
            $"{context.ServiceName}.Domain must not define catalog-forbidden technical concepts. "
            + $"Found: {string.Join(", ", violations.Select(type => type.FullName))}");
    }

    private static void AssertNoForbiddenDependencies(string assemblyName, IEnumerable<string> forbiddenReferences)
    {
        string[] referencedAssemblies = [.. LoadAssembly(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)];

        foreach (string forbiddenReference in forbiddenReferences)
        {
            string[] violations = [.. referencedAssemblies
                .Where(reference => reference.StartsWith(forbiddenReference, StringComparison.Ordinal))];

            Assert.True(
                violations.Length == 0,
                $"{assemblyName} must not reference {forbiddenReference}. Found: {string.Join(", ", violations)}");
        }
    }

    private static void AssertProjectHasNoForbiddenReferences(
        BoundedContextDescriptor context,
        ArchitectureLayer layer,
        IEnumerable<string> forbiddenReferences)
    {
        ProjectModel project = LoadProject(context, layer);

        foreach (string forbiddenReference in forbiddenReferences)
        {
            string[] violations = [.. project.PackageAndFrameworkReferences
                .Where(reference => reference.StartsWith(forbiddenReference, StringComparison.Ordinal))];

            Assert.True(
                violations.Length == 0,
                $"{context.ServiceName}.{layer}.csproj must not reference {forbiddenReference}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static void AssertSourceFilesDoNotContainCodeTerms(
        BoundedContextDescriptor context,
        ArchitectureLayer layer,
        IEnumerable<string> forbiddenTerms)
    {
        string sourceDirectory = Path.Combine(
            ArchitectureTestPaths.ContextFolder(context),
            context.AssemblyName(layer));

        string[] sourceFiles = [.. Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(sourceFile => !sourceFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(sourceFile => !sourceFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))];

        foreach (string forbiddenTerm in forbiddenTerms)
        {
            string[] violations = [.. sourceFiles
                .Where(sourceFile => CSharpLexicalText.RemoveCommentsAndStrings(File.ReadAllText(sourceFile))
                    .Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase))
                .Select(sourceFile => Path.GetRelativePath(ArchitectureTestPaths.RepositoryRoot.FullName, sourceFile))];

            Assert.True(
                violations.Length == 0,
                $"{context.ServiceName}.{layer} must not contain forbidden code term {forbiddenTerm}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static ProjectModel LoadProject(BoundedContextDescriptor context, ArchitectureLayer layer)
        => new(ArchitectureTestPaths.ProjectPath(context, layer));

    private static bool IsServicePort(ReflectionType interfaceType)
    {
        string? assemblyName = interfaceType.Assembly.GetName().Name;

        return assemblyName is not null
            && (assemblyName.EndsWith(".Application", StringComparison.Ordinal)
                || assemblyName.EndsWith(".Domain", StringComparison.Ordinal));
    }

    private static ReflectionAssembly LoadAssembly(string assemblyName)
        => ReflectionAssembly.Load(assemblyName);
}
