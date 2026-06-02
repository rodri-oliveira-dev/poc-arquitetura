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
    private static readonly string[] ServiceNames = ["LedgerService", "BalanceService"];
    private static readonly string[] DomainForbiddenReferences =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Confluent.Kafka",
        "Google.Cloud.PubSub.V1"
    ];
    private static readonly string[] ApplicationForbiddenReferences =
    [
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.OpenApi",
        "Swashbuckle.AspNetCore",
        "Confluent.Kafka",
        "Google.Cloud.PubSub.V1"
    ];
    private static readonly string[] MessagingProviderNames = ["Kafka", "PubSub"];
    private static readonly string[] WorkerForbiddenReferences =
    [
        "Microsoft.OpenApi",
        "Swashbuckle.AspNetCore"
    ];
    private static readonly ReflectionAssembly[] ProductionAssemblies =
    [
        .. ServiceNames.SelectMany(serviceName => new[]
        {
            LoadAssembly($"{serviceName}.Domain"),
            LoadAssembly($"{serviceName}.Application"),
            LoadAssembly($"{serviceName}.Infrastructure"),
            LoadAssembly($"{serviceName}.Api"),
            LoadAssembly($"{serviceName}.Worker")
        })
    ];
    private static readonly ArchArchitecture Architecture = new ArchLoader()
        .LoadAssemblies(ProductionAssemblies)
        .Build();
    private static readonly DirectoryInfo RepositoryRoot = GetRepositoryRoot();

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_should_not_depend_on_web_ef_core_or_messaging_providers(string serviceName)
    {
        // Domain stays independent from framework and infrastructure concerns.
        AssertNoForbiddenDependencies($"{serviceName}.Domain", DomainForbiddenReferences);
        AssertProjectHasNoForbiddenReferences(serviceName, "Domain", DomainForbiddenReferences);
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
        AssertNoForbiddenDependencies($"{serviceName}.Application", ApplicationForbiddenReferences);
        AssertProjectHasNoForbiddenReferences(serviceName, "Application", ApplicationForbiddenReferences);
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
            .Check(Architecture);
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
            .Check(Architecture);

        AssertNoForbiddenDependencies($"{serviceName}.Worker", WorkerForbiddenReferences);
        AssertProjectHasNoForbiddenLayerReferences(serviceName, "Worker", ["Api"]);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Infrastructure_should_reference_ef_core_and_implement_repository_ports(string serviceName)
    {
        // Infrastructure owns EF Core and concrete adapters behind application or domain ports.
        AssertProjectReferencesPackage(serviceName, "Infrastructure", "Microsoft.EntityFrameworkCore");

        ReflectionType[] repositoryTypes = LoadAssembly($"{serviceName}.Infrastructure")
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => !type.IsNested)
            .Where(type => type.Namespace == $"{serviceName}.Infrastructure.Persistence.Repositories")
            .ToArray();

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

        foreach (string serviceName in ServiceNames)
        {
            services.Add(serviceName);
        }

        return services;
    }

    private static void AssertNoForbiddenDependencies(string assemblyName, IEnumerable<string> forbiddenReferences)
    {
        string[] referencedAssemblies = LoadAssembly(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        foreach (string forbiddenReference in forbiddenReferences)
        {
            string[] violations = referencedAssemblies
                .Where(reference => reference.StartsWith(forbiddenReference, StringComparison.Ordinal))
                .ToArray();

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
        string[] directReferences = LoadProject(serviceName, layerName)
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

        foreach (string forbiddenReference in forbiddenReferences)
        {
            string[] violations = directReferences
                .Where(reference => reference.StartsWith(forbiddenReference, StringComparison.Ordinal))
                .ToArray();

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
        string[] projectReferences = LoadProject(serviceName, layerName)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

        foreach (string forbiddenLayer in forbiddenLayers)
        {
            string forbiddenProject = $"{serviceName}.{forbiddenLayer}.csproj";
            string[] violations = projectReferences
                .Where(reference => reference.EndsWith(forbiddenProject, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.True(
                violations.Length == 0,
                $"{serviceName}.{layerName}.csproj must not reference {forbiddenProject}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static void AssertProjectReferencesPackage(string serviceName, string layerName, string packageName)
    {
        string[] packageReferences = LoadProject(serviceName, layerName)
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

        Assert.Contains(packageName, packageReferences);
    }

    private static void AssertSourceFilesDoNotContainProviderNames(string serviceName, string layerName)
    {
        string sourceDirectory = Path.Combine(RepositoryRoot.FullName, "src", $"{serviceName}.{layerName}");
        string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(sourceFile => !sourceFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(sourceFile => !sourceFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string providerName in MessagingProviderNames)
        {
            string[] violations = sourceFiles
                .Where(sourceFile => File.ReadAllText(sourceFile).Contains(providerName, StringComparison.OrdinalIgnoreCase))
                .Select(sourceFile => Path.GetRelativePath(RepositoryRoot.FullName, sourceFile))
                .ToArray();

            Assert.True(
                violations.Length == 0,
                $"{serviceName}.{layerName} must not name messaging provider {providerName}. "
                + $"Found: {string.Join(", ", violations)}");
        }
    }

    private static XDocument LoadProject(string serviceName, string layerName)
    {
        string projectPath = Path.Combine(
            RepositoryRoot.FullName,
            "src",
            $"{serviceName}.{layerName}",
            $"{serviceName}.{layerName}.csproj");

        return XDocument.Load(projectPath);
    }

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

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LedgerService.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!;
    }
}
