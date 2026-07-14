namespace ContainerBaselineValidator.Tests;

public sealed class ContainerBaselineValidatorTests
{
    [Fact]
    public void Valid_dockerfile_and_compose_should_pass()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        List<string> failures = Program.Validate(fixture.Root);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Valid_repository_root_should_be_canonicalized()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        string root = Program.ResolveRepositoryRoot(fixture.Root);

        Assert.Equal(Path.GetFullPath(fixture.Root), root);
    }

    [Fact]
    public void Relative_repository_root_should_be_accepted_when_it_points_to_repository()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        string current = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            string relative = Path.GetRelativePath(Environment.CurrentDirectory, fixture.Root);

            string root = Program.ResolveRepositoryRoot(relative);

            Assert.Equal(Path.GetFullPath(fixture.Root), root);
        }
        finally
        {
            Environment.CurrentDirectory = current;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRepositoryRoots))]
    public void Invalid_repository_root_should_be_rejected(string scenario)
    {
        using RootFixture fixture = RootFixture.Create(scenario);

        Assert.Throws<InvalidOperationException>(() => Program.ResolveRepositoryRoot(fixture.Path));
    }

    [Theory]
    [InlineData("src/demo/Demo.Api/Dockerfile")]
    [InlineData(@"src\demo\Demo.Api\Dockerfile")]
    public void Relative_paths_with_unix_or_windows_separators_should_resolve_inside_root(string relativePath)
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        string fullPath = Program.ResolvePathWithinRoot(fixture.Root, relativePath);

        Assert.Equal(Path.GetFullPath(Path.Combine(fixture.Root, "src/demo/Demo.Api/Dockerfile")), fullPath);
    }

    [Theory]
    [InlineData("../fora-do-repositorio")]
    [InlineData("../../etc/passwd")]
    [InlineData(@"..\..\Windows\System32")]
    [InlineData("caminho/normal/../../fora")]
    public void Traversal_paths_should_be_rejected(string relativePath)
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        Assert.Throws<InvalidOperationException>(() => Program.ResolvePathWithinRoot(fixture.Root, relativePath));
    }

    [Fact]
    public void Unix_absolute_path_should_be_rejected()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        Assert.Throws<InvalidOperationException>(() => Program.ResolvePathWithinRoot(fixture.Root, "/etc/passwd"));
    }

    [Fact]
    public void Windows_absolute_path_should_be_rejected()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        Assert.Throws<InvalidOperationException>(() => Program.ResolvePathWithinRoot(fixture.Root, @"C:\Windows\System32"));
    }

    [Fact]
    public void Similar_root_prefix_should_still_be_rejected()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        string sibling = fixture.Root + "-malicioso";
        Directory.CreateDirectory(sibling);
        try
        {
            string relative = Path.GetRelativePath(fixture.Root, sibling);

            Assert.Throws<InvalidOperationException>(() => Program.ResolvePathWithinRoot(fixture.Root, relative));
        }
        finally
        {
            Directory.Delete(sibling, recursive: true);
        }
    }

    [Fact]
    public void Project_reference_escaping_repository_should_be_rejected_before_reading_target()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        fixture.SetApplicationProjectReference("../../../../fora-do-repositorio/Fora.csproj");

        Assert.Throws<InvalidOperationException>(() => Program.Validate(fixture.Root));
    }

    [Fact]
    public void Dockerfile_copy_source_escaping_repository_should_be_rejected_before_restore_analysis()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        fixture.ReplaceDockerfileLine("COPY src/demo/Demo.Domain/Demo.Domain.csproj src/demo/Demo.Domain/", "COPY ../../fora-do-repositorio/Fora.csproj src/demo/Fora/");

        Assert.Throws<InvalidOperationException>(() => Program.Validate(fixture.Root));
    }

    [Theory]
    [InlineData(@"..\Demo.Domain\Demo.Domain.csproj")]
    [InlineData("../Demo.Domain/Demo.Domain.csproj")]
    public void Project_reference_separators_should_resolve_to_same_canonical_path(string projectReference)
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        fixture.SetApplicationProjectReference(projectReference);
        fixture.ReplaceDockerfileLine("COPY src/demo/Demo.Domain/Demo.Domain.csproj src/demo/Demo.Domain/", string.Empty);

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Contains(failures, failure => failure.Contains("projeto ausente: src/demo/Demo.Domain/Demo.Domain.csproj", StringComparison.OrdinalIgnoreCase));
        AssertNoRelativeSegments(failures);
    }

    [Fact]
    public void Transitive_project_references_should_require_canonical_project_copies()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        fixture.ReplaceDockerfileLine("COPY src/demo/Demo.Domain/Demo.Domain.csproj src/demo/Demo.Domain/", string.Empty);

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Contains(failures, failure => failure.Contains("projeto ausente: src/demo/Demo.Domain/Demo.Domain.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(failures, failure => failure.Contains("src/demo/Demo.Application/../Demo.Domain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Shared_project_reference_should_resolve_to_canonical_shared_path()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Worker);
        fixture.AddSharedKafkaWorkerDefaultsReferenceToExecutable();

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Contains(failures, failure => failure.Contains("projeto ausente: src/Shared/KafkaWorkerDefaults/KafkaWorkerDefaults.csproj", StringComparison.OrdinalIgnoreCase));
        AssertNoRelativeSegments(failures);
    }

    [Fact]
    public void Real_missing_project_copy_should_still_report_violation()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        fixture.ReplaceDockerfileLine("COPY src/demo/Demo.Domain/Demo.Domain.csproj src/demo/Demo.Domain/", string.Empty);

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Contains(failures, failure => failure.Contains("projeto ausente: src/demo/Demo.Domain/Demo.Domain.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void Invalid_fixture_should_report_expected_violation(string scenario, string expectedViolation)
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);
        ArrangeInvalidFixture(fixture, scenario);

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Contains(failures, failure => failure.Contains(expectedViolation, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Worker_using_api_runtime_image_should_fail()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Worker);
        fixture.ReplaceDockerfileText("mcr.microsoft.com/dotnet/runtime:10.0 AS final", "mcr.microsoft.com/dotnet/aspnet:10.0 AS final");

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Contains(failures, failure => failure.Contains("imagem final incorreta para Worker", StringComparison.OrdinalIgnoreCase));
    }

    public static TheoryData<string, string> InvalidFixtures()
    {
        return new TheoryData<string, string>
        {
            { "api-final-image", "imagem final incorreta para Api" },
            { "missing-app-user", "usuario final nao aprovado" },
            { "root-user", "USER root no estagio final" },
            { "latest-tag", "tag latest proibida" },
            { "missing-tag", "imagem sem tag ou digest" },
            { "container-name", "container_name definido" },
            { "unbound-port", "porta publicada sem bind em 127.0.0.1" },
            { "missing-port-env", "variavel LEDGER_SERVICE_HOST_PORT ausente" },
            { "missing-healthcheck", "healthcheck ausente" },
            { "missing-cpu-limit", "limite cpus ausente" },
            { "missing-memory-limit", "limite memory ausente" },
            { "missing-pids-limit", "limite pids ausente" },
            { "missing-transitive-project-copy", "projeto ausente: src/demo/Demo.Domain/Demo.Domain.csproj" },
            { "missing-code-directory-copy", "diretorio ausente antes do publish: src/demo/Demo.Domain/" },
            { "missing-dockerfile", "Dockerfile ao lado do executavel" },
            { "missing-build-target", "build.target inexistente missing" },
            { "masked-restore", "restore repetido/alternativo com ||" },
            { "unsafe-healthcheck-url", "healthcheck do ContainerHealthProbe usa contrato inseguro" },
        };
    }

    public static TheoryData<string> InvalidRepositoryRoots()
    {
        return
        [
            "missing",
            "file",
            "without-sentinels",
            "system-root",
        ];
    }

    private static void ArrangeInvalidFixture(Fixture fixture, string scenario)
    {
        switch (scenario)
        {
            case "api-final-image":
                fixture.ReplaceDockerfileText("mcr.microsoft.com/dotnet/aspnet:10.0 AS final", "mcr.microsoft.com/dotnet/runtime:10.0 AS final");
                break;
            case "missing-app-user":
                fixture.ReplaceDockerfileText("USER $APP_UID", string.Empty);
                break;
            case "root-user":
                fixture.ReplaceDockerfileText("USER $APP_UID", "USER root");
                break;
            case "latest-tag":
                fixture.ReplaceDockerfileText("mcr.microsoft.com/dotnet/sdk:10.0 AS build", "mcr.microsoft.com/dotnet/sdk:latest AS build");
                break;
            case "missing-tag":
                fixture.ReplaceDockerfileText("mcr.microsoft.com/dotnet/sdk:10.0 AS build", "mcr.microsoft.com/dotnet/sdk AS build");
                break;
            case "container-name":
                fixture.InsertComposeServiceLine("container_name: demo");
                break;
            case "unbound-port":
                fixture.ReplaceComposeText("127.0.0.1:${LEDGER_SERVICE_HOST_PORT:-5000}:8080", "${LEDGER_SERVICE_HOST_PORT:-5000}:8080");
                break;
            case "missing-port-env":
                fixture.ReplaceEnvText("LEDGER_SERVICE_HOST_PORT=5000", string.Empty);
                break;
            case "missing-healthcheck":
                fixture.RemoveComposeBlock("healthcheck:");
                break;
            case "missing-cpu-limit":
                fixture.RemoveComposeLine("cpus:");
                break;
            case "missing-memory-limit":
                fixture.RemoveComposeLine("memory:");
                break;
            case "missing-pids-limit":
                fixture.RemoveComposeLine("pids:");
                break;
            case "missing-transitive-project-copy":
                fixture.ReplaceDockerfileLine("COPY src/demo/Demo.Domain/Demo.Domain.csproj src/demo/Demo.Domain/", string.Empty);
                break;
            case "missing-code-directory-copy":
                fixture.ReplaceDockerfileLine("COPY src/demo/Demo.Domain/ ./src/demo/Demo.Domain/", string.Empty);
                break;
            case "missing-dockerfile":
                fixture.DeleteDockerfile();
                break;
            case "missing-build-target":
                fixture.ReplaceComposeText("target: final", "target: missing");
                break;
            case "masked-restore":
                fixture.ReplaceDockerfileText("dotnet restore src/demo/Demo.Api/Demo.Api.csproj", "dotnet restore src/demo/Demo.Api/Demo.Api.csproj || dotnet restore src/demo/Demo.Api/Demo.Api.csproj");
                break;
            case "unsafe-healthcheck-url":
                fixture.ReplaceComposeText("\"/healthprobe/ContainerHealthProbe.dll\", \"8080\", \"/ready\"", "\"/healthprobe/ContainerHealthProbe.dll\", \"8080\", \"http://127.0.0.1:8080/ready\"");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown invalid fixture scenario.");
        }
    }

    private static void AssertNoRelativeSegments(IEnumerable<string> failures)
    {
        Assert.DoesNotContain(failures, failure => failure.Contains("/../", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("/./", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains(@"\..", StringComparison.Ordinal));
    }

    private enum ProjectKind
    {
        Api,
        Worker,
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _projectDirectory;
        private readonly string _projectName;
        private bool _disposed;

        private Fixture(string root, ProjectKind projectKind)
        {
            Root = root;
            ProjectKind = projectKind;
            _projectName = projectKind == ProjectKind.Api ? "Demo.Api" : "Demo.Worker";
            _projectDirectory = $"src/demo/{_projectName}";
        }

        public string Root
        {
            get;
        }

        private ProjectKind ProjectKind
        {
            get;
        }

        private string DockerfilePath => Path.Combine(Root, _projectDirectory, "Dockerfile");

        private string ComposePath => Path.Combine(Root, "compose.yaml");

        private string EnvPath => Path.Combine(Root, ".env.local.example");

        public static Fixture Create(ProjectKind projectKind)
        {
            string root = Path.Combine(Path.GetTempPath(), "container-baseline-tests-" + Guid.NewGuid().ToString("N"));
            Fixture fixture = new(root, projectKind);
            Directory.CreateDirectory(root);
            fixture.WriteRepositoryFiles();
            return fixture;
        }

        public void ReplaceDockerfileText(string oldValue, string newValue)
        {
            File.WriteAllText(DockerfilePath, File.ReadAllText(DockerfilePath).Replace(oldValue, newValue, StringComparison.Ordinal));
        }

        public void ReplaceDockerfileLine(string line, string replacement)
        {
            ReplaceLine(DockerfilePath, line, replacement);
        }

        public void ReplaceComposeText(string oldValue, string newValue)
        {
            File.WriteAllText(ComposePath, File.ReadAllText(ComposePath).Replace(oldValue, newValue, StringComparison.Ordinal));
        }

        public void ReplaceEnvText(string oldValue, string newValue)
        {
            File.WriteAllText(EnvPath, File.ReadAllText(EnvPath).Replace(oldValue, newValue, StringComparison.Ordinal));
        }

        public void InsertComposeServiceLine(string line)
        {
            ReplaceComposeText("    build:", $"    {line}{Environment.NewLine}    build:");
        }

        public void RemoveComposeLine(string lineStart)
        {
            string[] lines = File.ReadAllLines(ComposePath);
            File.WriteAllLines(ComposePath, lines.Where(line => !line.TrimStart().StartsWith(lineStart, StringComparison.Ordinal)));
        }

        public void RemoveComposeBlock(string lineStart)
        {
            string[] lines = File.ReadAllLines(ComposePath);
            int start = Array.FindIndex(lines, line => line.TrimStart().StartsWith(lineStart, StringComparison.Ordinal));
            Assert.True(start >= 0, $"Line '{lineStart}' was not found.");

            int end = start + 1;
            while (end < lines.Length && lines[end].StartsWith("      ", StringComparison.Ordinal))
            {
                end++;
            }

            File.WriteAllLines(ComposePath, lines[..start].Concat(lines[end..]));
        }

        public void DeleteDockerfile()
        {
            File.Delete(DockerfilePath);
        }

        public void SetApplicationProjectReference(string projectReference)
        {
            File.WriteAllText(Path.Combine(Root, "src/demo/Demo.Application/Demo.Application.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="{{projectReference}}" />
                  </ItemGroup>
                </Project>
                """);
        }

        public void AddSharedKafkaWorkerDefaultsReferenceToExecutable()
        {
            Directory.CreateDirectory(Path.Combine(Root, "src/Shared/KafkaWorkerDefaults"));
            File.WriteAllText(Path.Combine(Root, "src/Shared/KafkaWorkerDefaults/KafkaWorkerDefaults.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            File.WriteAllText(Path.Combine(Root, _projectDirectory, $"{_projectName}.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="../Demo.Application/Demo.Application.csproj" />
                    <ProjectReference Include="..\..\Shared\KafkaWorkerDefaults\KafkaWorkerDefaults.csproj" />
                  </ItemGroup>
                </Project>
                """);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private void WriteRepositoryFiles()
        {
            Directory.CreateDirectory(Path.Combine(Root, _projectDirectory));
            Directory.CreateDirectory(Path.Combine(Root, "src/demo/Demo.Application"));
            Directory.CreateDirectory(Path.Combine(Root, "src/demo/Demo.Domain"));

            File.WriteAllText(Path.Combine(Root, "global.json"), "{}");
            File.WriteAllText(Path.Combine(Root, "PocArquitetura.slnx"), "<Solution />");
            File.WriteAllText(Path.Combine(Root, "Directory.Packages.props"), "<Project />");
            File.WriteAllText(Path.Combine(Root, "Directory.Build.props"), "<Project />");
            File.WriteAllText(EnvPath, "LEDGER_SERVICE_HOST_PORT=5000");
            File.WriteAllText(Path.Combine(Root, _projectDirectory, $"{_projectName}.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="../Demo.Application/Demo.Application.csproj" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(Root, "src/demo/Demo.Application/Demo.Application.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="../Demo.Domain/Demo.Domain.csproj" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(Root, "src/demo/Demo.Domain/Demo.Domain.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(DockerfilePath, CreateDockerfile());
            File.WriteAllText(ComposePath, CreateCompose());
        }

        private string CreateDockerfile()
        {
            string finalImage = ProjectKind == ProjectKind.Api ? "aspnet" : "runtime";
            string project = $"{_projectDirectory}/{_projectName}.csproj";

            return $"""
                FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
                WORKDIR /src
                COPY global.json ./
                COPY Directory.Packages.props ./
                COPY Directory.Build.props ./
                COPY {_projectDirectory}/{_projectName}.csproj {_projectDirectory}/
                COPY src/demo/Demo.Application/Demo.Application.csproj src/demo/Demo.Application/
                COPY src/demo/Demo.Domain/Demo.Domain.csproj src/demo/Demo.Domain/
                RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked dotnet restore {project}
                COPY {_projectDirectory}/ ./{_projectDirectory}/
                COPY src/demo/Demo.Application/ ./src/demo/Demo.Application/
                COPY src/demo/Demo.Domain/ ./src/demo/Demo.Domain/
                RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked dotnet publish {project} -c Release -o /app/publish --no-restore /p:UseAppHost=false
                FROM mcr.microsoft.com/dotnet/{finalImage}:10.0 AS final
                WORKDIR /app
                COPY --from=build --chown=$APP_UID:0 /app/publish ./
                USER $APP_UID
                ENTRYPOINT ["dotnet", "{_projectName}.dll"]
                """;
        }

        private string CreateCompose()
        {
            string serviceName = ProjectKind == ProjectKind.Api ? "ledger-service" : "ledger-worker";

            return $$"""
                services:
                  {{serviceName}}:
                    build:
                      context: .
                      dockerfile: {{_projectDirectory}}/Dockerfile
                      target: final
                    ports:
                      - "127.0.0.1:${LEDGER_SERVICE_HOST_PORT:-5000}:8080"
                    healthcheck:
                      test: ["CMD", "dotnet", "/healthprobe/ContainerHealthProbe.dll", "8080", "/ready"]
                      interval: 10s
                      timeout: 5s
                      retries: 3
                    deploy:
                      resources:
                        limits:
                          cpus: "0.50"
                          memory: 256m
                          pids: 128
                """;
        }

        private static void ReplaceLine(string path, string line, string replacement)
        {
            string[] lines = File.ReadAllLines(path);
            int index = Array.FindIndex(lines, current => current.Trim().Equals(line, StringComparison.Ordinal));
            Assert.True(index >= 0, $"Line '{line}' was not found.");

            if (string.IsNullOrEmpty(replacement))
            {
                File.WriteAllLines(path, lines.Where((_, currentIndex) => currentIndex != index));
                return;
            }

            lines[index] = replacement;
            File.WriteAllLines(path, lines);
        }
    }

    private sealed class RootFixture : IDisposable
    {
        private RootFixture(string path, string? cleanupDirectory)
        {
            Path = path;
            CleanupDirectory = cleanupDirectory;
        }

        public string Path
        {
            get;
        }

        private string? CleanupDirectory
        {
            get;
        }

        public static RootFixture Create(string scenario)
        {
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "container-baseline-root-tests-" + Guid.NewGuid().ToString("N"));
            switch (scenario)
            {
                case "missing":
                    return new RootFixture(temp, null);
                case "file":
                    Directory.CreateDirectory(temp);
                    string file = System.IO.Path.Combine(temp, "root.txt");
                    File.WriteAllText(file, "not a directory");
                    return new RootFixture(file, temp);
                case "without-sentinels":
                    Directory.CreateDirectory(temp);
                    return new RootFixture(temp, temp);
                case "system-root":
                    string root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(temp))!;
                    return new RootFixture(root, null);
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown root scenario.");
            }
        }

        public void Dispose()
        {
            if (CleanupDirectory is not null && Directory.Exists(CleanupDirectory))
                Directory.Delete(CleanupDirectory, recursive: true);
        }
    }
}
