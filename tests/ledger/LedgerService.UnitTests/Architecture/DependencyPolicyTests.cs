using System.Xml.Linq;

namespace LedgerService.UnitTests.Architecture;

public sealed class DependencyPolicyTests
{
    private const string CryptographyXmlPackage = "System.Security.Cryptography.Xml";
    private const string OpenTelemetryApiPackage = "OpenTelemetry.Api";
    private const string OpenTelemetryOtlpExporterPackage = "OpenTelemetry.Exporter.OpenTelemetryProtocol";

    [Fact]
    public void Vulnerable_cryptography_xml_override_should_be_centralized()
    {
        var repositoryRoot = GetRepositoryRoot();
        var packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));

        var packageVersion = packages
            .Descendants("PackageVersion")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == CryptographyXmlPackage);
        Assert.NotNull(packageVersion);
        Assert.Equal("10.0.9", packageVersion!.Attribute("Version")!.Value);
    }

    [Fact]
    public void Vulnerable_opentelemetry_api_override_should_use_fixed_version()
    {
        var repositoryRoot = GetRepositoryRoot();
        var packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));

        var packageVersion = packages
            .Descendants("PackageVersion")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == OpenTelemetryApiPackage);
        Assert.NotNull(packageVersion);
        Assert.Equal("1.16.0", packageVersion!.Attribute("Version")!.Value);
    }

    [Fact]
    public void Otlp_exporter_should_be_centralized()
    {
        var repositoryRoot = GetRepositoryRoot();
        var packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));

        var packageVersion = packages
            .Descendants("PackageVersion")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == OpenTelemetryOtlpExporterPackage);
        Assert.NotNull(packageVersion);
        Assert.NotNull(packageVersion.Attribute("Version"));
    }

    [Fact]
    public void Dotnet_ci_should_block_moderate_or_higher_nuget_vulnerabilities()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));
        Assert.Contains("""blocked_severities = {"moderate", "high", "critical"}""", workflow);
    }

    [Fact]
    public void Dependency_review_should_block_moderate_or_higher_vulnerabilities()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dependency-review.yml"));
        Assert.Contains("fail-on-severity: moderate", workflow);
    }

    [Theory]
    [InlineData("src/ledger/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj")]
    [InlineData("src/balance/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj")]
    public void Infrastructure_projects_should_reference_cryptography_xml_without_local_version(string projectPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var project = XDocument.Load(Path.Combine(repositoryRoot.FullName, projectPath));

        var packageReference = project
            .Descendants("PackageReference")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == CryptographyXmlPackage);
        Assert.NotNull(packageReference);
        Assert.Null(packageReference!.Attribute("Version"));
        Assert.Equal("all", packageReference.Attribute("PrivateAssets")!.Value);
    }

    [Theory]
    [InlineData("src/ledger/LedgerService.Api/LedgerService.Api.csproj")]
    [InlineData("src/balance/BalanceService.Api/BalanceService.Api.csproj")]
    public void Api_projects_should_reference_otlp_exporter_without_local_version(string projectPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var project = XDocument.Load(Path.Combine(repositoryRoot.FullName, projectPath));

        var packageReference = project
            .Descendants("PackageReference")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == OpenTelemetryOtlpExporterPackage);
        Assert.NotNull(packageReference);
        Assert.Null(packageReference.Attribute("Version"));
    }

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LedgerService.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory;
    }
}
