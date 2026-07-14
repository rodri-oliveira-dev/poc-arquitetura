using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ContainerBaselineValidator;

internal static partial class Program
{
    private const string DockerfileName = "Dockerfile";
    private const string ImageKey = "image";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly string[] ComposeFiles = ["compose.yaml", "compose.k6.yaml", "compose.nginx.yaml", "compose.observability.yaml", "compose.sonar.yaml", "compose.cloudsql.yaml", "compose.kafka.yaml", "compose.pubsub.yaml"];
    private static readonly string[] ResourceLimitKeys = ["cpus", "memory", "pids"];
    private static readonly HashSet<string> ResourceLimitRequiredServices = ["postgres-db", "kafka", "kafka-init-topics", "pubsub-emulator", "pubsub-init", "mailpit", "ledger-service", "ledger-worker", "balance-service", "balance-worker", "transfer-service", "transfer-worker", "payment-service", "payment-worker", "audit-service", "audit-worker", "identity-service", "keycloak", "keycloak-identity-admin-init", "k6", "nginx-edge", "otel-collector", "jaeger", "prometheus", "grafana", "loki", "alloy", "alertmanager", "sonarqube", "sonar-db", "cloud-sql-proxy"];
    private static readonly HashSet<string> HttpApplicationServices = ["ledger-service", "balance-service", "transfer-service", "payment-service", "audit-service", "identity-service"];
    private static readonly Regex CopyRegex = new(@"^\s*COPY\s+(?:--[^\s]+\s+)*(?<source>[^\s\[]+)\s+(?<target>[^\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex FromRegex = new(@"^\s*FROM\s+(?<image>\S+)\s+AS\s+(?<stage>\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex PublishRegex = new(@"dotnet\s+publish\s+(?<project>\S+\.csproj)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex PortVariableRegex = new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)[:-]", RegexOptions.Compiled, RegexTimeout);

    public static int Main(string[] args)
    {
        var root = GetRepositoryRoot(args);
        if (args.Contains("--self-test-invalid", StringComparer.OrdinalIgnoreCase))
        {
            return RunInvalidFixtureSelfTest(root);
        }

        var failures = Validate(root);
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"Container baseline failed with {failures.Count} violation(s).");
            return 1;
        }

        Console.WriteLine("Container baseline validation passed.");
        return 0;
    }

    public static List<string> Validate(string root)
    {
        root = ResolveRepositoryRoot(root);
        var failures = new List<string>();
        ValidateDockerfiles(root, failures);
        ValidateCompose(root, failures);
        return failures;
    }

    private static void ValidateDockerfiles(string root, List<string> failures)
    {
        var sourceRoot = ResolvePathWithinRoot(root, "src");
        var dockerfiles = Directory.GetFiles(sourceRoot, DockerfileName, SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .Where(path => !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var executables = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .Where(path => path.EndsWith(".Api.csproj", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".Worker.csproj", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var executable in executables)
        {
            var expected = Normalize(Path.Combine(Path.GetDirectoryName(executable)!, DockerfileName));
            var matches = dockerfiles.Where(path => path.Equals(expected, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                failures.Add($"Dockerfile: {expected}; projeto publicado: {executable}; problema: esperado exatamente um Dockerfile ao lado do executavel; sugestao: mantenha um unico Dockerfile em {Path.GetDirectoryName(executable)}.");
            }
        }

        foreach (var dockerfile in dockerfiles)
        {
            var lines = File.ReadAllLines(ResolvePathWithinRoot(root, dockerfile));
            var text = string.Join('\n', lines);
            var publishProject = FindProject(lines, PublishRegex);
            if (publishProject is null)
            {
                failures.Add($"Dockerfile: {dockerfile}; problema: nenhum dotnet publish de executavel encontrado; sugestao: publique explicitamente o .csproj do executavel.");
                continue;
            }

            var projectKind = publishProject.Contains(".Api/", StringComparison.OrdinalIgnoreCase) || publishProject.EndsWith(".Api.csproj", StringComparison.OrdinalIgnoreCase) ? "Api" : "Worker";
            ValidateDockerfileStages(dockerfile, lines, text, projectKind, failures);
            ValidateProjectCopies(root, dockerfile, lines, publishProject, failures);
        }
    }

    private static void ValidateDockerfileStages(string dockerfile, string[] lines, string text, string projectKind, List<string> failures)
    {
        var finalFrom = lines.Select(line => FromRegex.Match(line)).LastOrDefault(match => match.Success);
        var expectedRuntime = projectKind == "Api" ? "/aspnet:" : "/runtime:";
        if (finalFrom is null || !finalFrom.Groups["image"].Value.Contains(expectedRuntime, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Dockerfile: {dockerfile}; problema: imagem final incorreta para {projectKind}; sugestao: APIs usam mcr.microsoft.com/dotnet/aspnet:<tag> e workers usam mcr.microsoft.com/dotnet/runtime:<tag>.");
        }

        AddFailureIf(!text.Contains("FROM mcr.microsoft.com/dotnet/sdk:", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: estagio de build SDK ausente; sugestao: use SDK somente no estagio build.", failures);
        AddFailureIf(!text.Contains("COPY global.json", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: global.json nao e copiado antes do restore; sugestao: adicione COPY global.json ./ antes de dotnet restore.", failures);
        AddFailureIf(!text.Contains("COPY Directory.Packages.props", StringComparison.OrdinalIgnoreCase) || !text.Contains("COPY Directory.Build.props", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: props centrais nao copiados antes do restore; sugestao: copie Directory.Packages.props e Directory.Build.props antes do restore.", failures);
        AddFailureIf(text.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase) && text.Contains("||", StringComparison.Ordinal), $"Dockerfile: {dockerfile}; problema: restore repetido/alternativo com ||; sugestao: corrija as copias de csproj antes do restore em vez de mascarar falhas.", failures);
        AddFailureIf(!text.Contains("--mount=type=cache", StringComparison.OrdinalIgnoreCase) || !text.Contains("target=/root/.nuget/packages", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: cache BuildKit de NuGet ausente; sugestao: use RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked.", failures);
        AddFailureIf(!text.Contains("--no-restore", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: publish sem --no-restore; sugestao: use dotnet publish --no-restore.", failures);
        AddFailureIf(!text.Contains("UseAppHost=false", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: UseAppHost=false ausente; sugestao: adicione /p:UseAppHost=false no publish.", failures);
        AddFailureIf(Regex.IsMatch(text, @"^\s*USER\s+root\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline, RegexTimeout), $"Dockerfile: {dockerfile}; problema: USER root no estagio final; sugestao: remova USER root e rode como usuario nao privilegiado.", failures);
        AddFailureIf(!Regex.IsMatch(text, @"^\s*USER\s+\$APP_UID\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline, RegexTimeout), $"Dockerfile: {dockerfile}; problema: usuario final nao aprovado; sugestao: use USER $APP_UID nas imagens .NET finais.", failures);
        AddFailureIf(!text.Contains("COPY --from=build --chown=$APP_UID:0", StringComparison.OrdinalIgnoreCase), $"Dockerfile: {dockerfile}; problema: COPY final sem --chown; sugestao: use COPY --from=build --chown=$APP_UID:0.", failures);
        AddFailureIf(Regex.IsMatch(text, @"^\s*(ARG|ENV)\s+.*(SECRET|PASSWORD|TOKEN|KEY)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline, RegexTimeout), $"Dockerfile: {dockerfile}; problema: possivel secret em ARG/ENV; sugestao: nao coloque secrets em build args, env, imagem ou contexto.", failures);
        foreach (Match from in lines.Select(line => Regex.Match(line, @"^\s*FROM\s+(?<image>\S+)", RegexOptions.IgnoreCase, RegexTimeout)).Where(match => match.Success))
            ValidateImageReference($"Dockerfile: {dockerfile}", from.Groups[ImageKey].Value, failures);
    }

    private static void AddFailureIf(bool condition, string failure, List<string> failures)
    {
        if (condition)
            failures.Add(failure);
    }

    private static void ValidateProjectCopies(string root, string dockerfile, string[] lines, string publishProject, List<string> failures)
    {
        var restoreIndex = Array.FindIndex(lines, line => line.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase));
        var publishIndex = Array.FindIndex(lines, line => line.Contains("dotnet publish", StringComparison.OrdinalIgnoreCase));
        if (restoreIndex < 0 || publishIndex < 0)
            return;

        var allReferences = GetTransitiveProjectReferences(root, publishProject);
        var csprojCopiesBeforeRestore = GetCopySources(root, lines.Take(restoreIndex + 1)).Where(source => source.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directoryCopiesBeforePublish = GetCopySources(root, lines.Take(publishIndex + 1)).Where(source => source.EndsWith("/", StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in allReferences)
        {
            if (!csprojCopiesBeforeRestore.Contains(reference.Project))
            {
                failures.Add($"Dockerfile: {dockerfile}; projeto ausente: {reference.Project}; origem: {reference.Origin}; sugestao: adicione COPY {reference.Project} {Normalize(Path.GetDirectoryName(reference.Project)!)} antes do dotnet restore.");
            }

            var projectDirectory = Normalize(Path.GetDirectoryName(reference.Project)!) + "/";
            if (!directoryCopiesBeforePublish.Contains(projectDirectory) && !directoryCopiesBeforePublish.Contains("./" + projectDirectory))
            {
                failures.Add($"Dockerfile: {dockerfile}; diretorio ausente antes do publish: {projectDirectory}; origem: {reference.Origin}; sugestao: adicione COPY {projectDirectory} ./{projectDirectory} antes do dotnet publish.");
            }
        }
    }

    private static void ValidateCompose(string root, List<string> failures)
    {
        var envVariables = ReadEnvExampleVariables(ResolvePathWithinRoot(root, ".env.local.example"));
        foreach (var composeFile in ComposeFiles.Where(file => File.Exists(ResolvePathWithinRoot(root, file))))
        {
            var compose = ReadYaml(ResolvePathWithinRoot(root, composeFile));
            if (!compose.TryGetValue("services", out var servicesNode) || servicesNode is not Dictionary<object, object> services)
                continue;

            foreach (var (serviceNameObject, serviceObject) in services)
            {
                var serviceName = serviceNameObject.ToString()!;
                if (serviceObject is not Dictionary<object, object> service)
                    continue;

                if (service.ContainsKey("container_name"))
                    failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: container_name definido; sugestao: remova container_name para permitir nomes gerenciados pelo Compose.");

                if (TryGetString(service, ImageKey, out var image))
                    ValidateImageReference($"Compose: {composeFile}; servico: {serviceName}", image, failures);

                ValidatePorts(composeFile, serviceName, service, envVariables, failures);
                ValidateBuild(root, composeFile, serviceName, service, failures);
                ValidateHealthcheck(composeFile, serviceName, service, failures);
                ValidateResourceLimits(composeFile, serviceName, service, failures);
            }
        }
    }

    private static void ValidateBuild(string root, string composeFile, string serviceName, Dictionary<object, object> service, List<string> failures)
    {
        if (!service.TryGetValue("build", out var buildNode))
            return;

        var build = buildNode as Dictionary<object, object>;
        var context = ResolveBuildContext(build);
        var dockerfile = ResolveBuildDockerfile(buildNode, build);
        var contextPath = ResolvePathWithinRoot(root, context);
        var dockerfilePath = ResolvePathWithinBase(root, contextPath, dockerfile);
        if (!File.Exists(dockerfilePath))
        {
            failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: Dockerfile inexistente {dockerfile}; sugestao: corrija build.dockerfile para um caminho versionado.");
            return;
        }

        if (build is not null && TryGetString(build, "target", out var target))
        {
            var hasTarget = File.ReadLines(dockerfilePath).Any(line => Regex.IsMatch(line, @$"^\s*FROM\s+\S+\s+AS\s+{Regex.Escape(target)}\s*$", RegexOptions.IgnoreCase, RegexTimeout));
            if (!hasTarget)
                failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: build.target inexistente {target}; sugestao: alinhe target com um estagio AS do Dockerfile.");
        }
    }

    private static string ResolveBuildContext(Dictionary<object, object>? build)
    {
        if (build is null)
            return ".";

        return TryGetString(build, "context", out var contextValue) ? contextValue : ".";
    }

    private static string ResolveBuildDockerfile(object buildNode, Dictionary<object, object>? build)
    {
        if (build is null)
            return buildNode.ToString() ?? DockerfileName;

        return TryGetString(build, "dockerfile", out var dockerfile) ? dockerfile : DockerfileName;
    }

    private static void ValidatePorts(string composeFile, string serviceName, Dictionary<object, object> service, HashSet<string> envVariables, List<string> failures)
    {
        if (!service.TryGetValue("ports", out var portsNode) || portsNode is not IEnumerable<object> ports)
            return;

        foreach (var portObject in ports)
        {
            var port = portObject.ToString()!;
            if (!port.StartsWith("127.0.0.1:", StringComparison.Ordinal))
                failures.Add($"Compose: {composeFile}; servico: {serviceName}; porta: {port}; problema: porta publicada sem bind em 127.0.0.1; sugestao: use \"127.0.0.1:${{VAR:-porta}}:container\".");

            foreach (Match match in PortVariableRegex.Matches(port))
            {
                var variable = match.Groups["name"].Value;
                if (!envVariables.Contains(variable))
                    failures.Add($"Compose: {composeFile}; servico: {serviceName}; porta: {port}; problema: variavel {variable} ausente em .env.local.example; sugestao: documente a variavel em .env.local.example.");
            }
        }
    }

    private static void ValidateHealthcheck(string composeFile, string serviceName, Dictionary<object, object> service, List<string> failures)
    {
        if (HttpApplicationServices.Contains(serviceName) && service.ContainsKey("build") && !service.ContainsKey("healthcheck"))
        {
            failures.Add($"Compose: {composeFile}; servico HTTP: {serviceName}; problema: healthcheck ausente; sugestao: configure healthcheck apontando para /ready.");
            return;
        }

        if (!HttpApplicationServices.Contains(serviceName) || !service.TryGetValue("healthcheck", out var healthcheckNode) || healthcheckNode is not Dictionary<object, object> healthcheck)
            return;

        if (!healthcheck.TryGetValue("test", out var testNode))
            return;

        var command = FlattenYamlSequence(testNode).Select(node => node.ToString() ?? string.Empty).ToArray();
        var probeIndex = Array.FindIndex(command, part => part.EndsWith("ContainerHealthProbe.dll", StringComparison.OrdinalIgnoreCase));
        if (probeIndex < 0)
            return;

        var arguments = command.Skip(probeIndex + 1).ToArray();
        if (arguments.Length != 2 || arguments[0] != "8080" || arguments[1] != "/ready")
            failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: healthcheck do ContainerHealthProbe usa contrato inseguro; sugestao: use porta 8080 e caminho relativo /ready.");
    }

    private static void ValidateResourceLimits(string composeFile, string serviceName, Dictionary<object, object> service, List<string> failures)
    {
        if (!ResourceLimitRequiredServices.Contains(serviceName) || (!service.ContainsKey("image") && !service.ContainsKey("build")))
            return;

        var limits = GetMap(service, "deploy", "resources", "limits");
        foreach (var key in ResourceLimitKeys.Where(key => limits is null || !limits.ContainsKey(key)))
        {
            failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: limite {key} ausente; sugestao: declare deploy.resources.limits.{key} conforme politica local.");
        }
    }

    private static List<ProjectReference> GetTransitiveProjectReferences(string root, string project)
    {
        var result = new List<ProjectReference> { new(project, project) };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<ProjectReference>();
        queue.Enqueue(new ProjectReference(project, project));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Project))
                continue;

            var fullPath = ResolvePathWithinRoot(root, current.Project);
            if (!File.Exists(fullPath))
                continue;

            var document = LoadXml(fullPath);
            var includes = document
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value));

            foreach (var include in includes)
            {
                var referenced = ResolveProjectReference(root, current.Project, include!);
                queue.Enqueue(new ProjectReference(referenced, current.Project));
                result.Add(new ProjectReference(referenced, current.Project));
            }
        }

        return [.. result.DistinctBy(reference => reference.Project, StringComparer.OrdinalIgnoreCase)];
    }

    private static string ResolveProjectReference(string repositoryRoot, string currentProject, string projectReference)
    {
        var currentProjectFullPath = ResolvePathWithinRoot(repositoryRoot, currentProject);
        var currentProjectDirectory = Path.GetDirectoryName(currentProjectFullPath)!;
        var referencedProjectFullPath = ResolvePathWithinBase(repositoryRoot, currentProjectDirectory, projectReference);

        return Normalize(Path.GetRelativePath(repositoryRoot, referencedProjectFullPath));
    }

    private static string? FindProject(string[] lines, Regex regex)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var window = string.Join(' ', lines.Skip(i).Take(6)).Replace("\\", " ");
            var match = regex.Match(window);
            if (match.Success)
                return Normalize(match.Groups["project"].Value.Trim('"', '\''));
        }

        return null;
    }

    private static IEnumerable<string> GetCopySources(string root, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = CopyRegex.Match(line);
            if (match.Success)
            {
                var source = match.Groups["source"].Value.Trim('"', '\'');
                var fullPath = ResolvePathWithinRoot(root, source);
                var normalized = Normalize(Path.GetRelativePath(root, fullPath)).TrimEnd('/');
                if (source.EndsWith("/", StringComparison.Ordinal) || source.EndsWith("\\", StringComparison.Ordinal))
                    normalized += "/";
                yield return normalized;
            }
        }
    }

    private static void ValidateImageReference(string prefix, string image, List<string> failures)
    {
        if (image.Contains("${", StringComparison.Ordinal))
            return;
        var imageSegments = image.Split('/');
        var lastSegment = imageSegments[^1];
        if (!lastSegment.Contains(':', StringComparison.Ordinal) && !lastSegment.Contains('@', StringComparison.Ordinal))
            failures.Add($"{prefix}; imagem: {image}; problema: imagem sem tag ou digest; sugestao: use uma tag explicita diferente de latest.");
        if (lastSegment.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
            failures.Add($"{prefix}; imagem: {image}; problema: tag latest proibida; sugestao: fixe uma tag explicita.");
    }

    private static Dictionary<object, object> ReadYaml(string path)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var yaml = File.ReadAllText(path);
        yaml = Regex.Replace(yaml, @":\s*!reset\s*$", ":", RegexOptions.Multiline, RegexTimeout);
        yaml = Regex.Replace(yaml, @"!reset\s+", string.Empty, RegexOptions.None, RegexTimeout);
        try
        {
            return deserializer.Deserialize<Dictionary<object, object>>(yaml) ?? [];
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"Arquivo YAML invalido: {Normalize(path)}.", ex);
        }
    }

    private static HashSet<string> ReadEnvExampleVariables(string path)
    {
        return File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#') && line.Contains('='))
            .Select(line => line.Split('=', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<object, object>? GetMap(Dictionary<object, object> root, params string[] keys)
    {
        Dictionary<object, object>? current = root;
        foreach (var key in keys)
        {
            if (current is null || !current.TryGetValue(key, out var node))
                return null;
            current = node as Dictionary<object, object>;
        }

        return current;
    }

    private static bool TryGetString(Dictionary<object, object> map, string key, out string value)
    {
        if (map.TryGetValue(key, out var node) && node is not null)
        {
            value = node.ToString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static IEnumerable<object> FlattenYamlSequence(object node)
    {
        if (node is string)
        {
            yield return node;
            yield break;
        }

        if (node is IEnumerable<object> sequence)
        {
            foreach (var item in sequence)
            {
                foreach (var nested in FlattenYamlSequence(item))
                    yield return nested;
            }

            yield break;
        }

        yield return node;
    }

    private static int RunInvalidFixtureSelfTest(string root)
    {
        var temp = Path.Combine(Path.GetTempPath(), "container-baseline-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            CopyFile(root, temp, "PocArquitetura.slnx");
            CopyFile(root, temp, "Directory.Packages.props");
            CopyFile(root, temp, "Directory.Build.props");
            CopyFile(root, temp, "global.json");
            CopyFile(root, temp, ".env.local.example");
            Directory.CreateDirectory(Path.Combine(temp, "src/demo/Demo.Api"));
            Directory.CreateDirectory(Path.Combine(temp, "src/demo/Demo.Application"));
            File.WriteAllText(ResolvePathWithinRoot(temp, "src/demo/Demo.Api/Demo.Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><ProjectReference Include=\"../Demo.Application/Demo.Application.csproj\" /></ItemGroup></Project>");
            File.WriteAllText(ResolvePathWithinRoot(temp, "src/demo/Demo.Application/Demo.Application.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(ResolvePathWithinRoot(temp, "src/demo/Demo.Api/Dockerfile"), """
                FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
                WORKDIR /src
                COPY Directory.Packages.props ./
                COPY Directory.Build.props ./
                COPY src/demo/Demo.Api/Demo.Api.csproj src/demo/Demo.Api/
                RUN dotnet restore src/demo/Demo.Api/Demo.Api.csproj || dotnet restore src/demo/Demo.Api/Demo.Api.csproj
                COPY src/demo/Demo.Api/ ./src/demo/Demo.Api/
                RUN dotnet publish src/demo/Demo.Api/Demo.Api.csproj -c Release -o /app/publish
                FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
                WORKDIR /app
                COPY --from=build /app/publish ./
                USER root
                ENTRYPOINT ["dotnet", "Demo.Api.dll"]
                """);
            File.WriteAllText(ResolvePathWithinRoot(temp, "compose.yaml"), """
                services:
                  demo-service:
                    image: demo:latest
                    container_name: demo
                    build:
                      context: .
                      dockerfile: src/demo/Demo.Api/Dockerfile
                    ports:
                      - "${DEMO_PORT:-5000}:8080"
                    deploy:
                      resources:
                        limits:
                          cpus: "0.50"
                """);

            var failures = Validate(temp);
            if (failures.Count == 0)
            {
                Console.Error.WriteLine("Self-test invalid fixture did not fail.");
                return 1;
            }

            Console.WriteLine($"Invalid fixture detected {failures.Count} expected violation(s).");
            return 0;
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void CopyFile(string root, string temp, string relative)
    {
        var source = ResolvePathWithinRoot(root, relative);
        var destination = ResolvePathWithinRoot(temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    private static string GetRepositoryRoot(string[] args)
    {
        var rootIndex = Array.FindIndex(args, arg => arg.Equals("--root", StringComparison.OrdinalIgnoreCase));
        if (rootIndex >= 0 && rootIndex + 1 < args.Length)
            return ResolveRepositoryRoot(args[rootIndex + 1]);

        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
            directory = directory.Parent;
        return ResolveRepositoryRoot(directory?.FullName ?? Environment.CurrentDirectory);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');

    public static string ResolveRepositoryRoot(string candidateRoot)
    {
        if (string.IsNullOrWhiteSpace(candidateRoot))
            throw new InvalidOperationException("A raiz do repositorio nao foi informada.");

        var fullPath = Path.GetFullPath(candidateRoot);
        if (!Directory.Exists(fullPath))
            throw new InvalidOperationException("A raiz informada nao existe ou nao e um diretorio.");

        var directory = new DirectoryInfo(fullPath);
        if (directory.Parent is null)
            throw new InvalidOperationException("A raiz do sistema nao pode ser usada como raiz do repositorio.");

        return !File.Exists(Path.Combine(fullPath, "PocArquitetura.slnx")) || !File.Exists(Path.Combine(fullPath, "global.json"))
            ? throw new InvalidOperationException("A raiz informada nao contem os arquivos sentinela esperados do repositorio.")
            : Path.TrimEndingDirectorySeparator(fullPath);
    }

    public static string ResolvePathWithinRoot(string repositoryRoot, string relativePath)
    {
        var root = Path.GetFullPath(repositoryRoot);
        return ContainsParentDirectorySegment(relativePath)
            ? throw new InvalidOperationException("O caminho informado escapa da raiz autorizada do repositorio.")
            : ResolvePathWithinBase(root, root, relativePath);
    }

    private static string ResolvePathWithinBase(string repositoryRoot, string basePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("O caminho relativo nao foi informado.");

        var normalizedRelativePath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedRelativePath) || IsWindowsDrivePath(normalizedRelativePath))
            throw new InvalidOperationException("O caminho informado deve ser relativo a raiz autorizada do repositorio.");

        var root = Path.GetFullPath(repositoryRoot);
        var fullBasePath = Path.GetFullPath(basePath);
        var candidate = Path.GetFullPath(Path.Combine(fullBasePath, normalizedRelativePath));
        var relative = Path.GetRelativePath(root, candidate);

        var escapesRoot = relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative);

        return escapesRoot
            ? throw new InvalidOperationException("O caminho informado escapa da raiz autorizada do repositorio.")
            : candidate;
    }

    private static XDocument LoadXml(string fullPath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(fullPath, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static bool ContainsParentDirectorySegment(string path)
    {
        return path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("..", StringComparison.Ordinal));
    }

    private static bool IsWindowsDrivePath(string path)
    {
        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    private sealed record ProjectReference(string Project, string Origin);
}
