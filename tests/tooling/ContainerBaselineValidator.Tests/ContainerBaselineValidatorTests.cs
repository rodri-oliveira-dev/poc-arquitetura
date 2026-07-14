namespace ContainerBaselineValidator.Tests;

public sealed class ContainerBaselineValidatorTests
{
    [Fact]
    public void Valid_dockerfile_and_compose_should_pass()
    {
        using Fixture fixture = Fixture.Create(ProjectKind.Api);

        List<string> failures = Program.Validate(fixture.Root);

        Assert.Empty(failures);
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
        };
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
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown invalid fixture scenario.");
        }
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
                      test: ["CMD", "dotnet", "ContainerHealthProbe.dll", "http://127.0.0.1:8080/ready"]
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
}
