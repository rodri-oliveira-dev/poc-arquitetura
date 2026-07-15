using System.Diagnostics;
using System.Text;

namespace LedgerService.UnitTests.Architecture;

public sealed class OwaspZapScriptBehaviorTests : IDisposable
{
    private readonly DirectoryInfo _repositoryRoot = GetRepositoryRoot();
    private readonly string _bashPath = ResolveBashPath();
    private readonly string _runId = Guid.NewGuid().ToString("N");
    private readonly string _relativeRunRoot;

    public OwaspZapScriptBehaviorTests()
    {
        _relativeRunRoot = $"artifacts/zap-script-tests/{_runId}";
        Directory.CreateDirectory(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot));
    }

    [Fact]
    public void Compose_network_scan_should_use_openapi_server_override_and_preserve_operational_failures()
    {
        var result = RunZapScript(
            [
                "--output-root", $"./{_relativeRunRoot}/reports",
                "--health-timeout", "1",
                "--health-interval", "0",
                "--docker-network", "poc-arquitetura_poc-net",
                "--ledger-url", "http://localhost:5226",
                "--balance-url", "http://localhost:5228",
                "--ledger-zap-url", "http://ledger-service:8080",
                "--balance-zap-url", "http://balance-service:8080",
            ],
            new Dictionary<string, string>
            {
                ["ZAP_LEDGER_EXIT_CODE"] = "3",
                ["ZAP_BALANCE_NO_SERVERS"] = "true",
            });

        Assert.True(
            result.ExitCode == 3,
            $"Exit code esperado: 3. Obtido: {result.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        var scanCommands = File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "scan-commands.log"));
        Assert.Equal(2, scanCommands.Length);
        Assert.Contains(scanCommands, command => command.Contains("-t http://ledger-service:8080/swagger/v1/swagger.json", StringComparison.Ordinal)
            && command.Contains("-O http://ledger-service:8080", StringComparison.Ordinal));
        Assert.Contains(scanCommands, command => command.Contains("-t http://balance-service:8080/swagger/v1/swagger.json", StringComparison.Ordinal)
            && command.Contains("-O http://balance-service:8080", StringComparison.Ordinal));
        Assert.DoesNotContain(scanCommands, command => command.Contains("-O http://ledger-service:8080/swagger/v1/swagger.json", StringComparison.Ordinal));
        Assert.DoesNotContain(scanCommands, command => command.Contains("-O http://balance-service:8080/swagger/v1/swagger.json", StringComparison.Ordinal));

        Assert.Contains("Servidor declarado no OpenAPI difere do servidor efetivo acessivel pelo container ZAP.", result.StandardError);
        Assert.Contains("Servidor declarado no documento: <ausente>", result.StandardError);

        var reportDirectory = Directory.GetDirectories(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "reports")).Single();
        var ledgerLog = File.ReadAllText(Path.Combine(reportDirectory, "ledger-service-api.log"));
        var balanceLog = File.ReadAllText(Path.Combine(reportDirectory, "balance-service-api.log"));
        Assert.Contains("stdout for http://ledger-service:8080/swagger/v1/swagger.json", ledgerLog);
        Assert.Contains("stderr for http://ledger-service:8080/swagger/v1/swagger.json", ledgerLog);
        Assert.Contains("stdout for http://balance-service:8080/swagger/v1/swagger.json", balanceLog);
        Assert.Contains("stderr for http://balance-service:8080/swagger/v1/swagger.json", balanceLog);

        var summary = File.ReadAllText(Path.Combine(reportDirectory, "summary.md"));
        Assert.Contains("LedgerService.Api", summary);
        Assert.Contains("BalanceService.Api", summary);
        Assert.Contains("failed-operational", summary);
        Assert.Contains("Servidor efetivo para o ZAP (-O): `http://ledger-service:8080`", summary);
        Assert.Contains("Servidor efetivo para o ZAP (-O): `http://balance-service:8080`", summary);
    }

    [Fact]
    public void Localhost_legacy_scan_should_keep_host_gateway_conversion_for_zap_container()
    {
        var result = RunZapScript(
            [
                "--output-root", $"./{_relativeRunRoot}/legacy-reports",
                "--health-timeout", "1",
                "--health-interval", "0",
            ],
            new Dictionary<string, string>());

        Assert.True(
            result.ExitCode == 0,
            $"Exit code esperado: 0. Obtido: {result.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        var scanCommands = File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "scan-commands.log"));
        Assert.Contains(scanCommands, command => command.Contains("-O http://host.docker.internal:5226", StringComparison.Ordinal));
        Assert.Contains(scanCommands, command => command.Contains("-O http://host.docker.internal:5228", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        var path = Path.Combine(_repositoryRoot.FullName, _relativeRunRoot);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private ProcessResult RunZapScript(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment)
    {
        var fakeBin = Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "fakebin");
        Directory.CreateDirectory(fakeBin);
        WriteFakeExecutable(Path.Combine(fakeBin, "curl"), FakeCurlScript);
        WriteFakeExecutable(Path.Combine(fakeBin, "docker"), FakeDockerScript);

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("set -e; ");
        commandBuilder.Append("chmod +x ");
        commandBuilder.Append(ShellQuote(ToBashPath(Path.Combine(fakeBin, "curl"))));
        commandBuilder.Append(' ');
        commandBuilder.Append(ShellQuote(ToBashPath(Path.Combine(fakeBin, "docker"))));
        commandBuilder.Append("; ");
        commandBuilder.Append("export PATH=");
        commandBuilder.Append(ShellQuote(ToBashPath(fakeBin)));
        commandBuilder.Append(":\"$PATH\"; ");
        commandBuilder.Append("export ZAP_FAKE_LOG=");
        commandBuilder.Append(ShellQuote($"./{_relativeRunRoot}/docker.log"));
        commandBuilder.Append("; ");
        commandBuilder.Append("export ZAP_SCAN_COMMANDS=");
        commandBuilder.Append(ShellQuote($"./{_relativeRunRoot}/scan-commands.log"));
        commandBuilder.Append("; ");
        foreach (var pair in environment)
        {
            commandBuilder.Append("export ");
            commandBuilder.Append(pair.Key);
            commandBuilder.Append('=');
            commandBuilder.Append(ShellQuote(pair.Value));
            commandBuilder.Append("; ");
        }

        commandBuilder.Append("curl() { return 0; }; ");
        commandBuilder.Append("docker() { bash ");
        commandBuilder.Append(ShellQuote(ToBashPath(Path.Combine(fakeBin, "docker"))));
        commandBuilder.Append(" \"$@\"; }; ");
        commandBuilder.Append("python3() { python \"$@\"; }; ");
        commandBuilder.Append("export -f curl docker python3; ");
        commandBuilder.Append("bash ./scripts/security/run-owasp-zap.sh");
        foreach (var argument in arguments)
        {
            commandBuilder.Append(' ');
            commandBuilder.Append(ShellQuote(argument));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _bashPath,
            WorkingDirectory = _repositoryRoot.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(commandBuilder.ToString());

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static void WriteFakeExecutable(string path, string content)
    {
        File.WriteAllText(path, content.ReplaceLineEndings("\n"), Encoding.UTF8);
    }

    private static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private static string ToBashPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Length >= 3 && normalized[1] == ':' && normalized[2] == '/'
            ? "/" + char.ToLowerInvariant(normalized[0]) + normalized[2..]
            : normalized;
    }

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory;
    }

    private static string ResolveBashPath()
    {
        if (!OperatingSystem.IsWindows())
            return "bash";

        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("GIT_BASH"),
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files\Git\usr\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return candidate;
        }

        return "bash";
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private const string FakeCurlScript = """
#!/usr/bin/env bash
exit 0
""";

    private const string FakeDockerScript = """
#!/usr/bin/env bash
set -euo pipefail

printf 'docker' >> "${ZAP_FAKE_LOG:?}"
for arg in "$@"; do
  printf ' %q' "$arg" >> "$ZAP_FAKE_LOG"
done
printf '\n' >> "$ZAP_FAKE_LOG"

case "${1:-}" in
  version)
    exit 0
    ;;
  compose)
    if [[ "${2:-}" == "version" ]]; then exit 0; fi
    ;;
  image)
    exit 0
    ;;
  network)
    exit 0
    ;;
  rm)
    exit 0
    ;;
  run)
    volume=""
    previous=""
    for arg in "$@"; do
      if [[ "$previous" == "-v" ]]; then
        volume="$arg"
      fi
      previous="$arg"
    done

    workdir="${volume%:/zap/wrk:rw}"

    for arg in "$@"; do
      if [[ "$arg" == "sh" ]]; then
        mkdir -p "$workdir"
        touch "$workdir/.zap-write-test.fake"
        rm -f "$workdir/.zap-write-test.fake"
        exit 0
      fi
    done

    for arg in "$@"; do
      if [[ "$arg" == "python3" ]]; then
        target="${@: -1}"
        if [[ "$target" == *"balance"* && "${ZAP_BALANCE_NO_SERVERS:-}" == "true" ]]; then
          echo 'DECLARED_SERVERS=[]'
        elif [[ "$target" == *"balance"* ]]; then
          echo 'DECLARED_SERVERS=["http://localhost:5228"]'
        else
          echo 'DECLARED_SERVERS=["http://localhost:5226"]'
        fi
        exit 0
      fi
    done

    for arg in "$@"; do
      if [[ "$arg" == "zap-api-scan.py" ]]; then
        target=""
        override=""
        html=""
        json=""
        markdown=""
        previous=""
        for scan_arg in "$@"; do
          case "$previous" in
            -t) target="$scan_arg" ;;
            -O) override="$scan_arg" ;;
            -r) html="$scan_arg" ;;
            -J) json="$scan_arg" ;;
            -w) markdown="$scan_arg" ;;
          esac
          previous="$scan_arg"
        done

        echo "zap-api-scan.py -t $target -O $override" >> "${ZAP_SCAN_COMMANDS:?}"
        mkdir -p "$workdir"
        [[ -n "$html" ]] && printf '<html></html>\n' > "$workdir/$html"
        [[ -n "$json" ]] && printf '{}\n' > "$workdir/$json"
        [[ -n "$markdown" ]] && printf '# report\n' > "$workdir/$markdown"
        echo "stdout for $target"
        echo "stderr for $target" >&2

        if [[ "$target" == *"ledger"* ]]; then
          exit "${ZAP_LEDGER_EXIT_CODE:-0}"
        fi
        exit "${ZAP_BALANCE_EXIT_CODE:-0}"
      fi
    done
    ;;
esac

echo "fake docker: unsupported command $*" >&2
exit 99
""";
}
