using System.Text.RegularExpressions;
using System.Xml.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ContainerBaselineValidator;

internal static partial class Program
{
    private static readonly string[] ComposeFiles = ["compose.yaml", "compose.k6.yaml", "compose.nginx.yaml", "compose.observability.yaml", "compose.sonar.yaml", "compose.cloudsql.yaml", "compose.kafka.yaml", "compose.pubsub.yaml"];
    private static readonly HashSet<string> ResourceLimitRequiredServices = ["postgres-db", "kafka", "kafka-init-topics", "pubsub-emulator", "pubsub-init", "mailpit", "ledger-service", "ledger-worker", "balance-service", "balance-worker", "transfer-service", "transfer-worker", "payment-service", "payment-worker", "audit-service", "audit-worker", "identity-service", "keycloak", "keycloak-identity-admin-init", "k6", "nginx-edge", "otel-collector", "jaeger", "prometheus", "grafana", "loki", "alloy", "alertmanager", "sonarqube", "sonar-db", "cloud-sql-proxy"];
    private static readonly HashSet<string> HttpApplicationServices = ["ledger-service", "balance-service", "transfer-service", "payment-service", "audit-service", "identity-service"];
    private static readonly Regex CopyRegex = new(@"^\s*COPY\s+(?:--[^\s]+\s+)*(?<source>[^\s\[]+)\s+(?<target>[^\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FromRegex = new(@"^\s*FROM\s+(?<image>\S+)\s+AS\s+(?<stage>\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RestoreRegex = new(@"dotnet\s+restore\s+(?<project>\S+\.csproj)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PublishRegex = new(@"dotnet\s+publish\s+(?<project>\S+\.csproj)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PortVariableRegex = new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)[:-]", RegexOptions.Compiled);

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
        var failures = new List<string>();
        ValidateDockerfiles(root, failures);
        ValidateCompose(root, failures);
        return failures;
    }

    private static void ValidateDockerfiles(string root, List<string> failures)
    {
        var dockerfiles = Directory.GetFiles(Path.Combine(root, "src"), "Dockerfile", SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .Where(path => !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var executables = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .Where(path => path.EndsWith(".Api.csproj", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".Worker.csproj", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var executable in executables)
        {
            var expected = Normalize(Path.Combine(Path.GetDirectoryName(executable)!, "Dockerfile"));
            var matches = dockerfiles.Where(path => path.Equals(expected, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                failures.Add($"Dockerfile: {expected}; projeto publicado: {executable}; problema: esperado exatamente um Dockerfile ao lado do executavel; sugestao: mantenha um unico Dockerfile em {Path.GetDirectoryName(executable)}.");
            }
        }

        foreach (var dockerfile in dockerfiles)
        {
            var lines = File.ReadAllLines(Path.Combine(root, dockerfile));
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
        var finalFrom = lines.Select(line => FromRegex.Match(line)).Where(match => match.Success).LastOrDefault();
        var expectedRuntime = projectKind == "Api" ? "/aspnet:" : "/runtime:";
        if (finalFrom is null || !finalFrom.Groups["image"].Value.Contains(expectedRuntime, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Dockerfile: {dockerfile}; problema: imagem final incorreta para {projectKind}; sugestao: APIs usam mcr.microsoft.com/dotnet/aspnet:<tag> e workers usam mcr.microsoft.com/dotnet/runtime:<tag>.");
        }

        if (!text.Contains("FROM mcr.microsoft.com/dotnet/sdk:", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: estagio de build SDK ausente; sugestao: use SDK somente no estagio build.");
        if (!text.Contains("COPY global.json", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: global.json nao e copiado antes do restore; sugestao: adicione COPY global.json ./ antes de dotnet restore.");
        if (!text.Contains("COPY Directory.Packages.props", StringComparison.OrdinalIgnoreCase) || !text.Contains("COPY Directory.Build.props", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: props centrais nao copiados antes do restore; sugestao: copie Directory.Packages.props e Directory.Build.props antes do restore.");
        if (text.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase) && text.Contains("||", StringComparison.Ordinal))
            failures.Add($"Dockerfile: {dockerfile}; problema: restore repetido/alternativo com ||; sugestao: corrija as copias de csproj antes do restore em vez de mascarar falhas.");
        if (!text.Contains("--mount=type=cache", StringComparison.OrdinalIgnoreCase) || !text.Contains("target=/root/.nuget/packages", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: cache BuildKit de NuGet ausente; sugestao: use RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked.");
        if (!text.Contains("--no-restore", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: publish sem --no-restore; sugestao: use dotnet publish --no-restore.");
        if (!text.Contains("UseAppHost=false", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: UseAppHost=false ausente; sugestao: adicione /p:UseAppHost=false no publish.");
        if (Regex.IsMatch(text, @"^\s*USER\s+root\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            failures.Add($"Dockerfile: {dockerfile}; problema: USER root no estagio final; sugestao: remova USER root e rode como usuario nao privilegiado.");
        if (!Regex.IsMatch(text, @"^\s*USER\s+\$APP_UID\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            failures.Add($"Dockerfile: {dockerfile}; problema: usuario final nao aprovado; sugestao: use USER $APP_UID nas imagens .NET finais.");
        if (!text.Contains("COPY --from=build --chown=$APP_UID:0", StringComparison.OrdinalIgnoreCase))
            failures.Add($"Dockerfile: {dockerfile}; problema: COPY final sem --chown; sugestao: use COPY --from=build --chown=$APP_UID:0.");
        if (Regex.IsMatch(text, @"^\s*(ARG|ENV)\s+.*(SECRET|PASSWORD|TOKEN|KEY)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            failures.Add($"Dockerfile: {dockerfile}; problema: possivel secret em ARG/ENV; sugestao: nao coloque secrets em build args, env, imagem ou contexto.");
        foreach (Match from in lines.Select(line => Regex.Match(line, @"^\s*FROM\s+(?<image>\S+)", RegexOptions.IgnoreCase)).Where(match => match.Success))
            ValidateImageReference($"Dockerfile: {dockerfile}", from.Groups["image"].Value, failures);
    }

    private static void ValidateProjectCopies(string root, string dockerfile, string[] lines, string publishProject, List<string> failures)
    {
        var restoreIndex = Array.FindIndex(lines, line => line.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase));
        var publishIndex = Array.FindIndex(lines, line => line.Contains("dotnet publish", StringComparison.OrdinalIgnoreCase));
        if (restoreIndex < 0 || publishIndex < 0)
            return;

        var allReferences = GetTransitiveProjectReferences(root, publishProject);
        var csprojCopiesBeforeRestore = GetCopySources(lines.Take(restoreIndex + 1)).Where(source => source.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directoryCopiesBeforePublish = GetCopySources(lines.Take(publishIndex + 1)).Where(source => source.EndsWith("/", StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        var envVariables = ReadEnvExampleVariables(Path.Combine(root, ".env.local.example"));
        foreach (var composeFile in ComposeFiles.Where(file => File.Exists(Path.Combine(root, file))))
        {
            var compose = ReadYaml(Path.Combine(root, composeFile));
            if (!compose.TryGetValue("services", out var servicesNode) || servicesNode is not Dictionary<object, object> services)
                continue;

            foreach (var (serviceNameObject, serviceObject) in services)
            {
                var serviceName = serviceNameObject.ToString()!;
                if (serviceObject is not Dictionary<object, object> service)
                    continue;

                if (service.ContainsKey("container_name"))
                    failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: container_name definido; sugestao: remova container_name para permitir nomes gerenciados pelo Compose.");

                if (TryGetString(service, "image", out var image))
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
        var context = build is null ? "." : TryGetString(build, "context", out var contextValue) ? contextValue : ".";
        var dockerfile = build is null ? buildNode.ToString() ?? "Dockerfile" : TryGetString(build, "dockerfile", out var value) ? value : "Dockerfile";
        var dockerfilePath = Path.Combine(root, context.Replace('/', Path.DirectorySeparatorChar), dockerfile.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(dockerfilePath))
        {
            failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: Dockerfile inexistente {dockerfile}; sugestao: corrija build.dockerfile para um caminho versionado.");
            return;
        }

        if (build is not null && TryGetString(build, "target", out var target))
        {
            var hasTarget = File.ReadLines(dockerfilePath).Any(line => Regex.IsMatch(line, @$"^\s*FROM\s+\S+\s+AS\s+{Regex.Escape(target)}\s*$", RegexOptions.IgnoreCase));
            if (!hasTarget)
                failures.Add($"Compose: {composeFile}; servico: {serviceName}; problema: build.target inexistente {target}; sugestao: alinhe target com um estagio AS do Dockerfile.");
        }
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
            failures.Add($"Compose: {composeFile}; servico HTTP: {serviceName}; problema: healthcheck ausente; sugestao: configure healthcheck apontando para /ready.");
    }

    private static void ValidateResourceLimits(string composeFile, string serviceName, Dictionary<object, object> service, List<string> failures)
    {
        if (!ResourceLimitRequiredServices.Contains(serviceName) || (!service.ContainsKey("image") && !service.ContainsKey("build")))
            return;

        var limits = GetMap(service, "deploy", "resources", "limits");
        foreach (var key in new[] { "cpus", "memory", "pids" })
        {
            if (limits is null || !limits.ContainsKey(key))
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

            var fullPath = Path.Combine(root, current.Project.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                continue;

            var document = XDocument.Load(fullPath);
            foreach (var include in document.Descendants("ProjectReference").Select(element => element.Attribute("Include")?.Value).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var referenced = Normalize(Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, include!))));
                queue.Enqueue(new ProjectReference(referenced, current.Project));
                result.Add(new ProjectReference(referenced, current.Project));
            }
        }

        return result.DistinctBy(reference => reference.Project, StringComparer.OrdinalIgnoreCase).ToList();
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

    private static IEnumerable<string> GetCopySources(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = CopyRegex.Match(line);
            if (match.Success)
                yield return Normalize(match.Groups["source"].Value.Trim('"', '\''));
        }
    }

    private static void ValidateImageReference(string prefix, string image, List<string> failures)
    {
        if (image.Contains("${", StringComparison.Ordinal))
            return;
        var lastSegment = image.Split('/').Last();
        if (!lastSegment.Contains(':', StringComparison.Ordinal) && !lastSegment.Contains('@', StringComparison.Ordinal))
            failures.Add($"{prefix}; imagem: {image}; problema: imagem sem tag ou digest; sugestao: use uma tag explicita diferente de latest.");
        if (lastSegment.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
            failures.Add($"{prefix}; imagem: {image}; problema: tag latest proibida; sugestao: fixe uma tag explicita.");
    }

    private static Dictionary<object, object> ReadYaml(string path)
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(NullNamingConvention.Instance).Build();
        var yaml = File.ReadAllText(path);
        yaml = Regex.Replace(yaml, @":\s*!reset\s*$", ":", RegexOptions.Multiline);
        yaml = Regex.Replace(yaml, @"!reset\s+", string.Empty);
        return deserializer.Deserialize<Dictionary<object, object>>(yaml) ?? [];
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

    private static int RunInvalidFixtureSelfTest(string root)
    {
        var temp = Path.Combine(Path.GetTempPath(), "container-baseline-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            CopyFile(root, temp, "Directory.Packages.props");
            CopyFile(root, temp, "Directory.Build.props");
            CopyFile(root, temp, "global.json");
            CopyFile(root, temp, ".env.local.example");
            Directory.CreateDirectory(Path.Combine(temp, "src/demo/Demo.Api"));
            Directory.CreateDirectory(Path.Combine(temp, "src/demo/Demo.Application"));
            File.WriteAllText(Path.Combine(temp, "src/demo/Demo.Api/Demo.Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><ProjectReference Include=\"../Demo.Application/Demo.Application.csproj\" /></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(temp, "src/demo/Demo.Application/Demo.Application.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(temp, "src/demo/Demo.Api/Dockerfile"), """
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
            File.WriteAllText(Path.Combine(temp, "compose.yaml"), """
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
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(temp, relative))!);
        File.Copy(Path.Combine(root, relative), Path.Combine(temp, relative));
    }

    private static string GetRepositoryRoot(string[] args)
    {
        var rootIndex = Array.FindIndex(args, arg => arg.Equals("--root", StringComparison.OrdinalIgnoreCase));
        if (rootIndex >= 0 && rootIndex + 1 < args.Length)
            return Path.GetFullPath(args[rootIndex + 1]);

        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? Environment.CurrentDirectory;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');

    private sealed record ProjectReference(string Project, string Origin);
}
