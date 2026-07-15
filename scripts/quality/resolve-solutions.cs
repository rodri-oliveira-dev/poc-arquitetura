using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length == 1 && args[0] == "--self-test")
{
    return SelfTests.Run();
}

if (args.Length is < 3 or > 4)
{
    Console.Error.WriteLine("uso: dotnet run --file scripts/quality/resolve-solutions.cs -- <raiz-repositorio> <arquivo-alteracoes> <arquivo-saida-solutions> [sha-base]");
    Console.Error.WriteLine("teste: dotnet run --file scripts/quality/resolve-solutions.cs -- --self-test");
    return 1;
}

var repositoryRoot = Path.GetFullPath(args[0]);
var changesFile = Path.GetFullPath(args[1]);
var outputFile = Path.GetFullPath(args[2]);
var baseSha = args.Length == 4 ? args[3] : null;

try
{
    var resolver = SolutionResolver.Create(repositoryRoot, baseSha);
    var records = ChangeParser.Parse(File.ReadAllBytes(changesFile));
    var result = resolver.Resolve(records);

    Directory.CreateDirectory(Path.GetDirectoryName(outputFile) ?? ".");
    File.WriteAllText(outputFile, string.Join(Environment.NewLine, result.Solutions) + (result.Solutions.Count > 0 ? Environment.NewLine : ""), Encoding.UTF8);

    ReportPrinter.Print(result);
    return result.UnresolvedPotentialDotnetFiles.Count == 0 ? 0 : 2;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
{
    Console.Error.WriteLine($"falha ao resolver solutions impactadas: {ex.Message}");
    return 1;
}

enum ChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    Unknown,
}

sealed record ChangeRecord(ChangeKind Kind, string Path, string? PreviousPath = null);

sealed record ProjectEntry(string ProjectPath, string ProjectDirectory, IReadOnlyList<string> Solutions);

sealed record FileResolution(
    string Path,
    string? ProjectPath,
    IReadOnlyList<string> Solutions,
    string Strategy,
    IReadOnlyList<string> Attempts,
    bool IsPotentialDotnet,
    bool IsUnresolved);

sealed record ResolveResult(IReadOnlyList<FileResolution> Files, IReadOnlyList<string> Solutions)
{
    public IReadOnlyList<FileResolution> UnresolvedPotentialDotnetFiles { get; } = Files
        .Where(file => file is { IsPotentialDotnet: true, IsUnresolved: true })
        .ToArray();
}

sealed class SolutionResolver
{
    private readonly string repositoryRoot;
    private readonly IReadOnlyList<ProjectEntry> projects;
    private readonly IReadOnlyDictionary<string, string> solutionPaths;

    private SolutionResolver(string repositoryRoot, IReadOnlyList<ProjectEntry> projects, IReadOnlyDictionary<string, string> solutionPaths)
    {
        this.repositoryRoot = repositoryRoot;
        this.projects = projects;
        this.solutionPaths = solutionPaths;
    }

    public static SolutionResolver Create(string repositoryRoot, string? baseSha)
    {
        var currentSolutions = Directory.EnumerateFiles(repositoryRoot, "*.slnx", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new SolutionDocument(NormalizeRelativePath(repositoryRoot, path), File.ReadAllText(path, Encoding.UTF8)))
            .ToList();

        var allSolutions = new List<SolutionDocument>(currentSolutions);
        if (!string.IsNullOrWhiteSpace(baseSha))
        {
            foreach (var solution in currentSolutions)
            {
                var historicalContent = TryReadGitFile(repositoryRoot, baseSha, solution.Path);
                if (!string.IsNullOrWhiteSpace(historicalContent))
                {
                    allSolutions.Add(new SolutionDocument(solution.Path, historicalContent));
                }
            }
        }

        var index = BuildProjectIndex(allSolutions);
        var solutionPaths = currentSolutions
            .Select(solution => solution.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => path, path => path, StringComparer.OrdinalIgnoreCase);

        return new SolutionResolver(repositoryRoot, index, solutionPaths);
    }

    public ResolveResult Resolve(IReadOnlyList<ChangeRecord> records)
    {
        var resolutions = new List<FileResolution>();
        var selectedSolutions = new SortedSet<string>(SolutionComparer.Instance);

        foreach (var record in records)
        {
            var paths = record.Kind == ChangeKind.Renamed && !string.IsNullOrWhiteSpace(record.PreviousPath)
                ? new[] { record.PreviousPath!, record.Path }
                : new[] { record.Path };

            foreach (var path in paths)
            {
                var resolution = ResolvePath(path, record.Kind);
                resolutions.Add(resolution);
                foreach (var solution in resolution.Solutions)
                {
                    selectedSolutions.Add(solution);
                }
            }
        }

        return new ResolveResult(resolutions, selectedSolutions.ToArray());
    }

    private FileResolution ResolvePath(string rawPath, ChangeKind kind)
    {
        var path = NormalizePath(rawPath);
        var attempts = new List<string>();

        attempts.Add("regras manuais para arquivos sem projeto proprietario");
        if (TryResolveManualRule(path, out var manualSolutions, out var manualStrategy))
        {
            return new FileResolution(path, null, manualSolutions, manualStrategy, attempts, IsPotentialDotnet(path), false);
        }

        attempts.Add("indice caminho .csproj -> solutions extraido das .slnx");
        var project = FindOwnerProject(path);
        if (project is not null)
        {
            var selected = SelectPreferredSolutions(project);
            return new FileResolution(path, project.ProjectPath, selected, "prefixo de diretorio de projeto mais especifico", attempts, IsPotentialDotnet(path), false);
        }

        attempts.Add("fallback conservador para arquivos .NET potencialmente impactantes");
        if (IsPotentialDotnet(path))
        {
            return new FileResolution(
                path,
                null,
                ExistingSolutions("PocArquitetura.slnx"),
                kind == ChangeKind.Deleted ? "arquivo removido sem projeto localizavel; fallback conservador" : "arquivo .NET sem projeto localizavel; fallback conservador",
                attempts,
                true,
                true);
        }

        return new FileResolution(path, null, Array.Empty<string>(), "sem impacto .NET local conhecido", attempts, false, false);
    }

    private ProjectEntry? FindOwnerProject(string path)
    {
        return projects
            .Where(project => IsSamePath(path, project.ProjectPath) || IsValidPrefix(project.ProjectDirectory, path))
            .OrderByDescending(project => project.ProjectDirectory.Length)
            .FirstOrDefault();
    }

    private IReadOnlyList<string> SelectPreferredSolutions(ProjectEntry project)
    {
        if (TryContextSolution(project.ProjectPath, out var contextSolution) && project.Solutions.Contains(contextSolution, StringComparer.OrdinalIgnoreCase))
        {
            return new[] { contextSolution };
        }

        if (IsUnder(project.ProjectPath, "src/Shared") || IsUnder(project.ProjectPath, "tests/Shared"))
        {
            return ExistingSolutions("PocArquitetura.Shared.slnx");
        }

        if (project.Solutions.Count == 1)
        {
            return project.Solutions;
        }

        var nonAggregate = project.Solutions
            .Where(solution => !solution.Equals("PocArquitetura.slnx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(solution => solution, SolutionComparer.Instance)
            .ToArray();

        return nonAggregate.Length > 0 ? nonAggregate : ExistingSolutions("PocArquitetura.slnx");
    }

    private bool TryResolveManualRule(string path, out IReadOnlyList<string> solutions, out string strategy)
    {
        strategy = "regra manual";

        if (IsDocumentationOrImage(path))
        {
            solutions = Array.Empty<string>();
            strategy = "documentacao/imagem sem impacto .NET local";
            return true;
        }

        if (MatchesAny(path, "global.json", "NuGet.config", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", ".editorconfig", ".globalconfig", ".githooks/pre-push"))
        {
            solutions = ExistingSolutions("PocArquitetura.Shared.slnx", "PocArquitetura.slnx");
            strategy = "configuracao .NET/analyzer global";
            return true;
        }

        if (MatchesAny(path, "dotnet-tools.json", ".config/dotnet-tools.json", "coverlet.runsettings", "test.sh", "test.ps1"))
        {
            solutions = ExistingSolutions("PocArquitetura.slnx");
            strategy = "tooling .NET validado pela agregadora";
            return true;
        }

        if (IsUnder(path, "tests/Architecture.Tests"))
        {
            solutions = ExistingSolutions("PocArquitetura.slnx");
            strategy = "testes arquiteturais transversais";
            return true;
        }

        if (IsUnder(path, "contracts/events"))
        {
            solutions = ExistingSolutions("LedgerService.slnx", "BalanceService.slnx");
            strategy = "contratos de eventos preservados do hook";
            return true;
        }

        if (IsUnder(path, "tools/ComposeEnvGen"))
        {
            solutions = ExistingSolutions("LedgerService.slnx");
            strategy = "tooling ComposeEnvGen preservado do hook";
            return true;
        }

        if (solutionPaths.ContainsKey(path))
        {
            solutions = ExistingSolutions(path);
            strategy = "arquivo de solution alterado";
            return true;
        }

        solutions = Array.Empty<string>();
        return false;
    }

    private string[] ExistingSolutions(params string[] names)
    {
        return names
            .Where(name => solutionPaths.ContainsKey(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, SolutionComparer.Instance)
            .ToArray();
    }

    private static ProjectEntry[] BuildProjectIndex(IEnumerable<SolutionDocument> solutions)
    {
        var byProject = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var solution in solutions)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(solution.Content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"nao foi possivel ler {solution.Path}: {ex.Message}", ex);
            }

            foreach (var project in document.Descendants("Project"))
            {
                var projectPath = NormalizePath(project.Attribute("Path")?.Value ?? "");
                if (string.IsNullOrWhiteSpace(projectPath) || !projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!byProject.TryGetValue(projectPath, out var solutionSet))
                {
                    solutionSet = new SortedSet<string>(SolutionComparer.Instance);
                    byProject[projectPath] = solutionSet;
                }

                solutionSet.Add(solution.Path);
            }
        }

        return byProject
            .Select(item => new ProjectEntry(item.Key, ProjectDirectory(item.Key), item.Value.ToArray()))
            .OrderByDescending(project => project.ProjectDirectory.Length)
            .ThenBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryContextSolution(string path, out string solution)
    {
        solution = "";
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || (!parts[0].Equals("src", StringComparison.OrdinalIgnoreCase) && !parts[0].Equals("tests", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (parts[1].Equals("Shared", StringComparison.OrdinalIgnoreCase))
        {
            solution = "PocArquitetura.Shared.slnx";
            return true;
        }

        if (parts[1].Equals("Architecture.Tests", StringComparison.OrdinalIgnoreCase))
        {
            solution = "PocArquitetura.slnx";
            return true;
        }

        solution = char.ToUpperInvariant(parts[1][0]) + parts[1][1..] + "Service.slnx";
        return true;
    }

    private static bool IsPotentialDotnet(string path)
    {
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return MatchesAny(path, "global.json", "NuGet.config", "Directory.Packages.props", ".editorconfig", ".globalconfig", "coverlet.runsettings", "test.sh", "test.ps1") ||
            IsUnder(path, "src") ||
            IsUnder(path, "tests") ||
            IsUnder(path, "tools") ||
            IsUnder(path, "contracts/events");
    }

    private static bool IsDocumentationOrImage(string path)
    {
        return path.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
            IsUnder(path, "docs") ||
            path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadGitFile(string repositoryRoot, string baseSha, string path)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.StartInfo.ArgumentList.Add("show");
        process.StartInfo.ArgumentList.Add($"{baseSha}:{path}");
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : null;
    }

    private static string ProjectDirectory(string projectPath)
    {
        var lastSlash = projectPath.LastIndexOf('/');
        return lastSlash < 0 ? "" : projectPath[..lastSlash];
    }

    private static bool IsValidPrefix(string prefix, string path)
    {
        return path.Length > prefix.Length &&
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            path[prefix.Length] == '/';
    }

    private static bool IsSamePath(string left, string right) => left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string directory)
    {
        var normalized = NormalizePath(directory);
        return path.Equals(normalized, StringComparison.OrdinalIgnoreCase) || IsValidPrefix(normalized, path);
    }

    private static bool MatchesAny(string path, params string[] values) => values.Any(value => path.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeRelativePath(string root, string path)
    {
        return NormalizePath(Path.GetRelativePath(root, path));
    }

    public static string NormalizePath(string path)
    {
        var value = path.Trim().Trim('\uFEFF').Replace('\\', '/');
        while (value.StartsWith("./", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return value.TrimStart('/');
    }

    private sealed record SolutionDocument(string Path, string Content);
}

sealed class SolutionComparer : IComparer<string>, IEqualityComparer<string>
{
    public static readonly SolutionComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var left = Order(x);
        var right = Order(y);
        var orderComparison = left.CompareTo(right);
        return orderComparison != 0 ? orderComparison : StringComparer.OrdinalIgnoreCase.Compare(x, y);
    }

    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);

    private static int Order(string value)
    {
        for (var index = 0; index < Preferred.Length; index++)
        {
            if (Preferred[index].Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return Preferred.Length;
    }

    private static readonly string[] Preferred =
    [
        "PocArquitetura.Shared.slnx",
        "AuditService.slnx",
        "IdentityService.slnx",
        "LedgerService.slnx",
        "BalanceService.slnx",
        "PaymentService.slnx",
        "TransferService.slnx",
        "PocArquitetura.slnx",
    ];
}

static class ChangeParser
{
    public static IReadOnlyList<ChangeRecord> Parse(byte[] data)
    {
        if (data.Contains((byte)0))
        {
            return ParseNameStatusZ(data);
        }

        var text = Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<ChangeRecord>();
        }

        if (text.TrimStart().StartsWith('['))
        {
            return ParseJson(text);
        }

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => new ChangeRecord(ChangeKind.Modified, SolutionResolver.NormalizePath(path)))
            .ToArray();
    }

    private static List<ChangeRecord> ParseNameStatusZ(byte[] data)
    {
        var tokens = Encoding.UTF8.GetString(data).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var records = new List<ChangeRecord>();

        for (var index = 0; index < tokens.Length;)
        {
            var status = tokens[index++];
            var kind = ParseStatus(status);
            if (kind is ChangeKind.Renamed or ChangeKind.Copied)
            {
                if (index + 1 >= tokens.Length)
                {
                    throw new InvalidOperationException("registro NUL de rename/copy incompleto");
                }

                var oldPath = SolutionResolver.NormalizePath(tokens[index++]);
                var newPath = SolutionResolver.NormalizePath(tokens[index++]);
                records.Add(new ChangeRecord(kind, newPath, oldPath));
                continue;
            }

            if (index >= tokens.Length)
            {
                throw new InvalidOperationException("registro NUL incompleto");
            }

            records.Add(new ChangeRecord(kind, SolutionResolver.NormalizePath(tokens[index++])));
        }

        return records;
    }

    private static List<ChangeRecord> ParseJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("entrada JSON deve ser uma lista");
        }

        var records = new List<ChangeRecord>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                records.Add(new ChangeRecord(ChangeKind.Modified, SolutionResolver.NormalizePath(item.GetString() ?? "")));
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("filename", out var filenameProperty) ||
                filenameProperty.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("itens JSON devem ser strings ou objetos com filename");
            }

            var path = SolutionResolver.NormalizePath(filenameProperty.GetString() ?? "");
            var status = item.TryGetProperty("status", out var statusProperty) && statusProperty.ValueKind == JsonValueKind.String
                ? statusProperty.GetString()
                : null;
            var kind = ParseStatus(status ?? "M");
            var previousPath = item.TryGetProperty("previous_filename", out var previousProperty) && previousProperty.ValueKind == JsonValueKind.String
                ? SolutionResolver.NormalizePath(previousProperty.GetString() ?? "")
                : null;

            if (!string.IsNullOrWhiteSpace(previousPath) && kind != ChangeKind.Renamed)
            {
                kind = ChangeKind.Renamed;
            }

            records.Add(new ChangeRecord(kind, path, previousPath));
        }

        return records;
    }

    private static ChangeKind ParseStatus(string status)
    {
        if (status.Equals("added", StringComparison.OrdinalIgnoreCase))
        {
            return ChangeKind.Added;
        }

        if (status.Equals("modified", StringComparison.OrdinalIgnoreCase))
        {
            return ChangeKind.Modified;
        }

        if (status.Equals("removed", StringComparison.OrdinalIgnoreCase) || status.Equals("deleted", StringComparison.OrdinalIgnoreCase))
        {
            return ChangeKind.Deleted;
        }

        if (status.Equals("renamed", StringComparison.OrdinalIgnoreCase))
        {
            return ChangeKind.Renamed;
        }

        if (status.Equals("copied", StringComparison.OrdinalIgnoreCase))
        {
            return ChangeKind.Copied;
        }

        return status.Length == 0 ? ChangeKind.Unknown : char.ToUpperInvariant(status[0]) switch
        {
            'A' => ChangeKind.Added,
            'M' => ChangeKind.Modified,
            'D' => ChangeKind.Deleted,
            'R' => ChangeKind.Renamed,
            'C' => ChangeKind.Copied,
            _ => ChangeKind.Unknown,
        };
    }
}

static class ReportPrinter
{
    public static void Print(ResolveResult result)
    {
        foreach (var file in result.Files)
        {
            Console.WriteLine(file.Path);
            Console.WriteLine($"  projeto: {file.ProjectPath ?? "(sem projeto proprietario)"}");
            Console.WriteLine($"  solution: {(file.Solutions.Count == 0 ? "(nenhuma)" : string.Join(", ", file.Solutions))}");
            Console.WriteLine($"  estrategia: {file.Strategy}");
            if (file.IsUnresolved)
            {
                Console.WriteLine($"  erro: arquivo .NET potencialmente impactante nao resolvido com seguranca; estrategias tentadas: {string.Join("; ", file.Attempts)}");
                Console.WriteLine("  fallback: PocArquitetura.slnx foi selecionada de forma conservadora quando disponivel");
            }
        }

        Console.WriteLine("solutions selecionadas:");
        if (result.Solutions.Count == 0)
        {
            Console.WriteLine("  (nenhuma)");
        }
        else
        {
            foreach (var solution in result.Solutions)
            {
                Console.WriteLine($"  {solution}");
            }
        }
    }
}

static class SelfTests
{
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "resolve-solutions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            CreateRepositoryFixture(root);
            var resolver = SolutionResolver.Create(root, null);

            AssertSolutions(resolver, "codigo audit", [new(ChangeKind.Modified, "src/audit/AuditService.Application/Handler.cs")], ["AuditService.slnx"]);
            AssertSolutions(resolver, "codigo balance", [new(ChangeKind.Modified, "src\\balance\\BalanceService.Application\\Handler.cs")], ["BalanceService.slnx"]);
            AssertSolutions(resolver, "codigo identity", [new(ChangeKind.Modified, "src/identity/IdentityService.Application/Handler.cs")], ["IdentityService.slnx"]);
            AssertSolutions(resolver, "codigo ledger", [new(ChangeKind.Modified, "src/ledger/LedgerService.Application/Handler.cs")], ["LedgerService.slnx"]);
            AssertSolutions(resolver, "codigo payment", [new(ChangeKind.Modified, "src/payment/PaymentService.Application/Handler.cs")], ["PaymentService.slnx"]);
            AssertSolutions(resolver, "codigo transfer", [new(ChangeKind.Modified, "src/transfer/TransferService.Application/Handler.cs")], ["TransferService.slnx"]);
            AssertSolutions(resolver, "teste audit", [new(ChangeKind.Modified, "tests/audit/AuditService.UnitTests/HandlerTests.cs")], ["AuditService.slnx"]);
            AssertSolutions(resolver, "teste balance", [new(ChangeKind.Modified, "tests/balance/BalanceService.UnitTests/HandlerTests.cs")], ["BalanceService.slnx"]);
            AssertSolutions(resolver, "teste identity", [new(ChangeKind.Modified, "tests/identity/IdentityService.UnitTests/HandlerTests.cs")], ["IdentityService.slnx"]);
            AssertSolutions(resolver, "teste ledger", [new(ChangeKind.Modified, "tests/ledger/LedgerService.UnitTests/HandlerTests.cs")], ["LedgerService.slnx"]);
            AssertSolutions(resolver, "teste payment", [new(ChangeKind.Modified, "tests/payment/PaymentService.UnitTests/HandlerTests.cs")], ["PaymentService.slnx"]);
            AssertSolutions(resolver, "teste transfer", [new(ChangeKind.Modified, "tests/transfer/TransferService.UnitTests/HandlerTests.cs")], ["TransferService.slnx"]);
            AssertSolutions(resolver, "contextual vence agregadora", [new(ChangeKind.Modified, "src/balance/BalanceService.Application/BalanceService.Application.csproj")], ["BalanceService.slnx"]);
            AssertSolutions(resolver, "shared vence multiplas", [new(ChangeKind.Modified, "src/Shared/ApiDefaults/Service.cs")], ["PocArquitetura.Shared.slnx"]);
            AssertSolutions(resolver, "arquivo global", [new(ChangeKind.Modified, "Directory.Build.props")], ["PocArquitetura.Shared.slnx", "PocArquitetura.slnx"]);
            AssertSolutions(resolver, "contrato evento", [new(ChangeKind.Modified, "contracts/events/ledger-entry-created/v1.json")], ["LedgerService.slnx", "BalanceService.slnx"]);
            AssertSolutions(resolver, "teste arquitetural", [new(ChangeKind.Modified, "tests/Architecture.Tests/Rules.cs")], ["PocArquitetura.slnx"]);
            AssertSolutions(resolver, "rename entre contextos", [new(ChangeKind.Renamed, "src/balance/BalanceService.Application/New Name.cs", "src/ledger/LedgerService.Application/Old Name.cs")], ["LedgerService.slnx", "BalanceService.slnx"]);
            AssertSolutions(resolver, "arquivo removido", [new(ChangeKind.Deleted, "src/payment/PaymentService.Application/Deleted Handler.cs")], ["PaymentService.slnx"]);
            AssertSolutions(resolver, "sem correspondencia", [new(ChangeKind.Modified, "assets/logo.png")], []);
            AssertSolutions(resolver, "espacos e deduplicacao", [new(ChangeKind.Modified, "src/balance/BalanceService.Application/File With Spaces.cs"), new(ChangeKind.Modified, "src/balance/BalanceService.Application/Other.cs")], ["BalanceService.slnx"]);
            AssertSolutions(resolver, "separadores mistos", [new(ChangeKind.Modified, ".\\tests\\Shared\\ApiDefaults.Tests\\My Test.cs")], ["PocArquitetura.Shared.slnx"]);

            var parsed = ChangeParser.Parse(Encoding.UTF8.GetBytes("R100\0src/ledger/LedgerService.Application/Old.cs\0src/balance/BalanceService.Application/New.cs\0"));
            AssertSolutions(resolver, "parser NUL rename", parsed, ["LedgerService.slnx", "BalanceService.slnx"]);

            Console.WriteLine("self-test: todos os cenarios passaram");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine($"self-test falhou: {ex.Message}");
            return 1;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertSolutions(SolutionResolver resolver, string name, IReadOnlyList<ChangeRecord> records, string[] expected)
    {
        var actual = resolver.Resolve(records).Solutions;
        if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{name}: esperado [{string.Join(", ", expected)}], obtido [{string.Join(", ", actual)}]");
        }
    }

    private static void CreateRepositoryFixture(string root)
    {
        WriteSlnx(root, "PocArquitetura.slnx",
        [
            "src/audit/AuditService.Application/AuditService.Application.csproj",
            "src/balance/BalanceService.Application/BalanceService.Application.csproj",
            "src/identity/IdentityService.Application/IdentityService.Application.csproj",
            "src/ledger/LedgerService.Application/LedgerService.Application.csproj",
            "src/payment/PaymentService.Application/PaymentService.Application.csproj",
            "src/transfer/TransferService.Application/TransferService.Application.csproj",
            "src/Shared/ApiDefaults/ApiDefaults.csproj",
            "tests/audit/AuditService.UnitTests/AuditService.UnitTests.csproj",
            "tests/balance/BalanceService.UnitTests/BalanceService.UnitTests.csproj",
            "tests/identity/IdentityService.UnitTests/IdentityService.UnitTests.csproj",
            "tests/ledger/LedgerService.UnitTests/LedgerService.UnitTests.csproj",
            "tests/payment/PaymentService.UnitTests/PaymentService.UnitTests.csproj",
            "tests/transfer/TransferService.UnitTests/TransferService.UnitTests.csproj",
            "tests/Shared/ApiDefaults.Tests/ApiDefaults.Tests.csproj",
            "tests/Architecture.Tests/Architecture.Tests.csproj",
        ]);

        foreach (var context in new[] { "Audit", "Balance", "Identity", "Ledger", "Payment", "Transfer" })
        {
            var key = context.ToLowerInvariant();
            WriteSlnx(root, $"{context}Service.slnx",
            [
                $"src/{key}/{context}Service.Application/{context}Service.Application.csproj",
                $"tests/{key}/{context}Service.UnitTests/{context}Service.UnitTests.csproj",
            ]);
        }

        WriteSlnx(root, "PocArquitetura.Shared.slnx",
        [
            "src/Shared/ApiDefaults/ApiDefaults.csproj",
            "tests/Shared/ApiDefaults.Tests/ApiDefaults.Tests.csproj",
        ]);
    }

    private static void WriteSlnx(string root, string fileName, IReadOnlyList<string> projects)
    {
        var projectsXml = string.Join(Environment.NewLine, projects.Select(path => $"  <Project Path=\"{path}\" />"));
        File.WriteAllText(Path.Combine(root, fileName), $"<Solution>{Environment.NewLine}{projectsXml}{Environment.NewLine}</Solution>", Encoding.UTF8);
    }
}
