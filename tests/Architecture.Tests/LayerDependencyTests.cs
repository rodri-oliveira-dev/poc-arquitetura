using System.Xml.Linq;

using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ArchArchitecture = ArchUnitNET.Domain.Architecture;
using ReflectionAssembly = System.Reflection.Assembly;
using ReflectionType = System.Type;

namespace Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly string[] _serviceNames = ["LedgerService", "BalanceService", "TransferService"];
    private static readonly string[] _servicesWithPersistence = ["LedgerService", "BalanceService"];
    private static readonly string[] _domainForbiddenReferences =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Confluent.Kafka",
        "Google.Cloud.PubSub.V1"
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
    private static readonly string[] _messagingProviderNames = ["Kafka", "PubSub"];
    private static readonly string[] _concreteKafkaProducerConsumerNames =
    [
        "KafkaOutboxMessagePublisher",
        "KafkaDeadLetterPublisher",
        "KafkaTransferenciaOutboxPublisher",
        "LedgerEventsConsumer",
        "ReprocessamentoLancamentosConsumerService",
        "ITransferenciaKafkaProducer"
    ];
    private static readonly string[] _workerForbiddenReferences =
    [
        "Microsoft.OpenApi",
        "Swashbuckle.AspNetCore"
    ];
    private static readonly ReflectionAssembly[] _productionAssemblies =
    [
        .. _serviceNames.SelectMany(serviceName => new[]
        {
            LoadAssembly($"{serviceName}.Domain"),
            LoadAssembly($"{serviceName}.Application"),
            LoadAssembly($"{serviceName}.Infrastructure"),
            LoadAssembly($"{serviceName}.Api"),
            LoadAssembly($"{serviceName}.Worker")
        })
    ];
    private static readonly ArchArchitecture _architecture = new ArchLoader()
        .LoadAssemblies(_productionAssemblies)
        .Build();
    private static readonly DirectoryInfo _repositoryRoot = GetRepositoryRoot();

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_should_not_depend_on_web_ef_core_or_messaging_providers(string serviceName)
    {
        // Domain stays independent from framework and infrastructure concerns.
        AssertNoForbiddenDependencies($"{serviceName}.Domain", _domainForbiddenReferences);
        AssertProjectHasNoForbiddenReferences(serviceName, "Domain", _domainForbiddenReferences);
        AssertProjectHasNoForbiddenLayerReferences(
            serviceName,
            "Domain",
            ["Api", "Application", "Infrastructure", "Worker"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Application_should_not_depend_on_http_swagger_or_messaging_providers(string serviceName)
    {
        // Application orchestrates use cases without transport or messaging implementations.
        AssertNoForbiddenDependencies($"{serviceName}.Application", _applicationForbiddenReferences);
        AssertProjectHasNoForbiddenReferences(serviceName, "Application", _applicationForbiddenReferences);
        AssertProjectHasNoForbiddenLayerReferences(serviceName, "Application", ["Api", "Infrastructure", "Worker"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_and_application_should_not_name_messaging_providers(string serviceName)
    {
        AssertSourceFilesDoNotContainProviderNames(serviceName, "Domain");
        AssertSourceFilesDoNotContainProviderNames(serviceName, "Application");
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Api_should_not_depend_on_concrete_repositories(string serviceName)
    {
        // APIs call application use cases instead of executing rules through concrete repositories.
        Types().That().ResideInAssembly($"{serviceName}.Api")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace($"{serviceName}.Infrastructure.Persistence.Repositories"))
            .Because($"{serviceName}.Api must not depend on concrete persistence repositories")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Api_should_not_depend_on_concrete_kafka_producers_or_consumers(string serviceName)
    {
        // APIs may configure application/infrastructure composition, but Kafka adapters stay outside HTTP entry points.
        AssertProjectHasNoForbiddenReferences(serviceName, "Api", ["Confluent.Kafka"]);
        AssertSourceFilesDoNotContain(serviceName, "Api", _concreteKafkaProducerConsumerNames);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Worker_should_not_depend_on_controllers_or_swagger(string serviceName)
    {
        // Workers are independent processes and must not reuse HTTP presentation code.
        Types().That().ResideInAssembly($"{serviceName}.Worker")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace($"{serviceName}.Api.Controllers")
                    .Or().ResideInNamespace($"{serviceName}.Api.Swagger"))
            .Because($"{serviceName}.Worker must not depend on controllers or Swagger types")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);

        AssertNoForbiddenDependencies($"{serviceName}.Worker", _workerForbiddenReferences);
        AssertProjectHasNoForbiddenLayerReferences(serviceName, "Worker", ["Api"]);
    }

    [Fact]
    public void TransferService_projects_should_reference_only_allowed_internal_layers()
    {
        AssertProjectReferencesOnlyInternalLayers("TransferService", "Domain", []);
        AssertProjectReferencesOnlyInternalLayers("TransferService", "Application", ["Domain"]);
        AssertProjectReferencesOnlyInternalLayers("TransferService", "Infrastructure", ["Application", "Domain"]);
        AssertProjectReferencesOnlyInternalLayers("TransferService", "Api", ["Application", "Infrastructure"]);
        AssertProjectReferencesOnlyInternalLayers("TransferService", "Worker", ["Application", "Infrastructure"]);
    }

    [Fact]
    public void TransferService_should_not_use_pubsub()
    {
        foreach (string layerName in new[] { "Api", "Application", "Domain", "Infrastructure", "Worker" })
        {
            AssertProjectHasNoForbiddenReferences("TransferService", layerName, ["Google.Cloud.PubSub.V1"]);
            AssertSourceFilesDoNotContain("TransferService", layerName, ["PubSub", "Pub/Sub", "Google.Cloud.PubSub"]);
        }
    }

    [Theory]
    [InlineData("LedgerService")]
    [InlineData("BalanceService")]
    public void PubSub_should_remain_only_in_legacy_worker_adapters_for_ledger_and_balance(string serviceName)
    {
        foreach (string layerName in new[] { "Api", "Application", "Domain", "Infrastructure" })
        {
            AssertProjectHasNoForbiddenReferences(serviceName, layerName, ["Google.Cloud.PubSub.V1"]);
            AssertSourceFilesDoNotContain(serviceName, layerName, ["PubSub", "Pub/Sub", "Google.Cloud.PubSub"]);
        }
    }

    [Fact]
    public void TransferService_api_should_not_register_worker_hosted_services()
    {
        AssertProjectHasNoForbiddenLayerReferences("TransferService", "Api", ["Worker"]);
        AssertSourceFilesDoNotContain("TransferService", "Api", ["AddHostedService", "BackgroundService"]);
    }

    [Fact]
    public void TransferService_worker_should_not_reference_controllers_or_swagger_sources()
    {
        AssertProjectHasNoForbiddenReferences("TransferService", "Worker", ["Swashbuckle.AspNetCore", "Microsoft.OpenApi"]);
        AssertSourceFilesDoNotContain("TransferService", "Worker", ["ControllerBase", "MapControllers", "Swagger"]);
    }

    [Theory]
    [MemberData(nameof(PersistentServices))]
    public void Infrastructure_should_reference_ef_core_and_implement_repository_ports(string serviceName)
    {
        // Infrastructure owns EF Core and concrete adapters behind application or domain ports.
        AssertProjectReferencesPackage(serviceName, "Infrastructure", "Microsoft.EntityFrameworkCore");

        ReflectionType[] repositoryTypes = [.. LoadAssembly($"{serviceName}.Infrastructure")
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => !type.IsNested)
            .Where(type => type.Namespace == $"{serviceName}.Infrastructure.Persistence.Repositories")];

        Assert.NotEmpty(repositoryTypes);

        foreach (ReflectionType repositoryType in repositoryTypes)
        {
            Assert.True(
                repositoryType.GetInterfaces().Any(IsServicePort),
                $"{repositoryType.FullName} must implement an application or domain port.");
        }
    }

    public static TheoryData<string> Services()
    {
        TheoryData<string> services = [];

        foreach (string serviceName in _serviceNames)
        {
            services.Add(serviceName);
        }

        return services;
    }

    public static TheoryData<string> PersistentServices()
    {
        TheoryData<string> services = [];

        foreach (string serviceName in _servicesWithPersistence)
        {
            services.Add(serviceName);
        }

        return services;
    }

    private static void AssertNoForbiddenDependencies(string assemblyName, IEnumerable<string> forbiddenReferences)
    {
        string[] referencedAssemblies = [.. LoadAssembly(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)];

        foreach (string forbiddenReference in forbiddenReferences)
        {
            string[] violations = [.. referencedAssemblies.Where(reference => reference.StartsWith(forbiddenReference, StringComparison.Ordinal))];

            Assert.True(
                violations.Length == 0,
                $"{assemblyName} must not reference {forbiddenReference}. Found: {string.Join(", ", violations)}");
        }
    }

    private static void AssertProjectHasNoForbiddenReferences(
        string serviceName,
        string layerName,
        IEnumerable<string> forbiddenReferences)
    {
        string[] directReferences = [.. LoadProject(serviceName, layerName)
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)];

        foreach (string forbiddenReference in forbiddenReferences)
        {
            string[] violations = [.. directReferences.Where(reference => reference.StartsWith(forbiddenReference, StringComparison.Ordinal))];

            Assert.True(
                violations.Length == 0,
                $"{serviceName}.{layerName}.csproj must not reference {forbiddenReference}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static void AssertProjectHasNoForbiddenLayerReferences(
        string serviceName,
        string layerName,
        IEnumerable<string> forbiddenLayers)
    {
        string[] projectReferences = [.. LoadProject(serviceName, layerName)
            .Descendants("ProjectReference")
            .Select(GetProjectReferenceFileName)
            .Where(reference => reference is not null)
            .Select(reference => reference!)];

        foreach (string forbiddenLayer in forbiddenLayers)
        {
            string forbiddenProject = $"{serviceName}.{forbiddenLayer}.csproj";
            string[] violations = [.. projectReferences.Where(reference => reference.EndsWith(forbiddenProject, StringComparison.OrdinalIgnoreCase))];

            Assert.True(
                violations.Length == 0,
                $"{serviceName}.{layerName}.csproj must not reference {forbiddenProject}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static void AssertProjectReferencesOnlyInternalLayers(
        string serviceName,
        string layerName,
        IEnumerable<string> allowedLayers)
    {
        string[] allowedProjects = [.. allowedLayers.Select(allowedLayer => $"{serviceName}.{allowedLayer}.csproj")];

        string[] internalProjectReferences = [.. LoadProject(serviceName, layerName)
            .Descendants("ProjectReference")
            .Select(GetProjectReferenceFileName)
            .Where(reference => reference is not null
                && reference.StartsWith($"{serviceName}.", StringComparison.OrdinalIgnoreCase))
            .Select(reference => reference!)
            .Order(StringComparer.OrdinalIgnoreCase)];

        Assert.Equal(
            allowedProjects.Order(StringComparer.OrdinalIgnoreCase),
            internalProjectReferences);
    }

    private static void AssertProjectReferencesPackage(string serviceName, string layerName, string packageName)
    {
        string[] packageReferences = [.. LoadProject(serviceName, layerName)
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)];

        Assert.Contains(packageName, packageReferences);
    }

    private static string? GetProjectReferenceFileName(XElement projectReference)
    {
        string normalizedPath = ((string?)projectReference.Attribute("Include") ?? string.Empty)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFileName(normalizedPath);
    }

    private static void AssertSourceFilesDoNotContainProviderNames(string serviceName, string layerName)
        => AssertSourceFilesDoNotContain(serviceName, layerName, _messagingProviderNames);

    private static void AssertSourceFilesDoNotContain(
        string serviceName,
        string layerName,
        IEnumerable<string> forbiddenTerms)
    {
        string sourceDirectory = Path.Combine(
            _repositoryRoot.FullName,
            "src",
            GetServiceFolderName(serviceName),
            $"{serviceName}.{layerName}");
        string[] sourceFiles = [.. Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(sourceFile => !sourceFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(sourceFile => !sourceFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))];

        foreach (string forbiddenTerm in forbiddenTerms)
        {
            string[] violations = [.. sourceFiles
                .Where(sourceFile => File.ReadAllText(sourceFile).Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase))
                .Select(sourceFile => Path.GetRelativePath(_repositoryRoot.FullName, sourceFile))];

            Assert.True(
                violations.Length == 0,
                $"{serviceName}.{layerName} must not contain forbidden term {forbiddenTerm}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static XDocument LoadProject(string serviceName, string layerName)
    {
        string projectPath = Path.Combine(
            _repositoryRoot.FullName,
            "src",
            GetServiceFolderName(serviceName),
            $"{serviceName}.{layerName}",
            $"{serviceName}.{layerName}.csproj");

        return XDocument.Load(projectPath);
    }

    private static string GetServiceFolderName(string serviceName)
        => serviceName switch
        {
            "LedgerService" => "ledger",
            "BalanceService" => "balance",
            "TransferService" => "transfer",
            _ => throw new ArgumentOutOfRangeException(nameof(serviceName), serviceName, "Unknown service name.")
        };

    private static bool IsServicePort(ReflectionType interfaceType)
    {
        string? assemblyName = interfaceType.Assembly.GetName().Name;

        return assemblyName is not null
            && (assemblyName.EndsWith(".Application", StringComparison.Ordinal)
                || assemblyName.EndsWith(".Domain", StringComparison.Ordinal));
    }

    private static ReflectionAssembly LoadAssembly(string assemblyName)
    {
        return ReflectionAssembly.Load(assemblyName);
    }

    private static DirectoryInfo GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory;
    }
}
