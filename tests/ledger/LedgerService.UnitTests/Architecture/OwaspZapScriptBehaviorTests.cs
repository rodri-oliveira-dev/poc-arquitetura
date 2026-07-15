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
                ["ZAP_FAKE_SETFACL_RESULT"] = "success",
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
        AssertExpectedZapArtifacts(reportDirectory);

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
    public void Zap_workdir_should_use_setfacl_when_acl_preparation_succeeds()
    {
        var result = RunZapScript(
            [
                "--output-root", $"./{_relativeRunRoot}/acl-reports",
                "--health-timeout", "1",
                "--health-interval", "0",
            ],
            new Dictionary<string, string>
            {
                ["ZAP_FAKE_SETFACL_RESULT"] = "success",
            });

        Assert.True(
            result.ExitCode == 0,
            $"Exit code esperado: 0. Obtido: {result.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        AssertFakeCommandsResolvedFromFakeBin();

        var reportDirectory = Directory.GetDirectories(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "acl-reports")).Single();
        var setfaclOperations = File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "setfacl.log"));
        var aclOperation = Assert.Single(setfaclOperations, operation => operation.StartsWith("u:1000:rwx|", StringComparison.Ordinal));
        var aclPath = aclOperation["u:1000:rwx|".Length..];

        Assert.Equal(NormalizePathForComparison(reportDirectory), NormalizePathForComparison(aclPath));
        Assert.True(File.Exists(Path.Combine(reportDirectory, ".fake-acl")));
        Assert.Equal("1000:rwx", File.ReadAllText(Path.Combine(reportDirectory, ".fake-acl")).Trim());

        var chmodOperations = File.Exists(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "chmod.log"))
            ? File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "chmod.log"))
            : [];
        Assert.DoesNotContain(chmodOperations, operation => operation.StartsWith("0777|", StringComparison.Ordinal));
    }

    [Fact]
    public void Zap_workdir_should_fallback_to_chmod_only_for_timestamped_output_directory_when_setfacl_fails()
    {
        var result = RunZapScript(
            [
                "--output-root", $"./{_relativeRunRoot}/permission-reports",
                "--health-timeout", "1",
                "--health-interval", "0",
            ],
            new Dictionary<string, string>
            {
                ["ZAP_FAKE_SETFACL_RESULT"] = "failure",
            });

        Assert.True(
            result.ExitCode == 0,
            $"Exit code esperado: 0. Obtido: {result.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        AssertFakeCommandsResolvedFromFakeBin();

        var reportDirectory = Directory.GetDirectories(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "permission-reports")).Single();
        var setfaclOperations = File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "setfacl.log"));
        Assert.Single(setfaclOperations, operation => operation.StartsWith("u:1000:rwx|", StringComparison.Ordinal));

        var chmodOperations = File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "chmod.log"));
        var widenedOperation = Assert.Single(chmodOperations, operation => operation.StartsWith("0777|", StringComparison.Ordinal));
        var widenedPath = widenedOperation["0777|".Length..];

        Assert.Equal(NormalizePathForComparison(reportDirectory), NormalizePathForComparison(widenedPath));
        Assert.NotEqual(NormalizePathForComparison(_repositoryRoot.FullName), NormalizePathForComparison(widenedPath));
        Assert.NotEqual(
            NormalizePathForComparison(Path.Combine(_repositoryRoot.FullName, "artifacts")),
            NormalizePathForComparison(widenedPath));
        Assert.NotEqual(
            NormalizePathForComparison(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "permission-reports")),
            NormalizePathForComparison(widenedPath));

        var dockerLog = File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "docker.log"));
        var firstWorkdirValidation = dockerLog.First(line => line.Contains("/zap/wrk", StringComparison.Ordinal) && line.Contains("mktemp", StringComparison.Ordinal));
        Assert.Contains("-v", firstWorkdirValidation);
        Assert.Contains(":/zap/wrk:rw", firstWorkdirValidation);
        Assert.DoesNotContain($"./{_relativeRunRoot}", firstWorkdirValidation);
        Assert.True(Path.IsPathFullyQualified(widenedPath), $"O diretorio preparado deveria ser absoluto: {widenedPath}");
    }

    [Fact]
    public void Zap_workdir_validation_should_fail_clearly_when_container_user_cannot_write()
    {
        var result = RunZapScript(
            [
                "--output-root", $"./{_relativeRunRoot}/unwritable-reports",
                "--health-timeout", "1",
                "--health-interval", "0",
            ],
            new Dictionary<string, string>
            {
                ["ZAP_FAKE_SETFACL_RESULT"] = "failure",
                ["ZAP_SKIP_CHMOD_MARKER"] = "true",
            });

        Assert.True(
            result.ExitCode == 1,
            $"Exit code esperado: 1. Obtido: {result.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        Assert.Contains("Falha operacional: /zap/wrk nao esta gravavel pelo usuario da imagem ZAP.", result.StandardError);
        Assert.Contains("Caminho absoluto montado:", result.StandardError);
        Assert.Contains("Ownership e permissoes no host:", result.StandardError);
        Assert.Contains("UID/GID usados pela imagem ZAP:", result.StandardError);
        Assert.Contains("uid=1000(zap) gid=1000(zap)", result.StandardError);
        Assert.Contains("Imagem ZAP: ghcr.io/zaproxy/zaproxy:stable", result.StandardError);
        Assert.Contains("Saida completa da validacao:", result.StandardError);
        Assert.Contains("Permission denied", result.StandardError);

        var scanCommandsPath = Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "scan-commands.log");
        Assert.False(File.Exists(scanCommandsPath), "O scan nao deveria iniciar quando a validacao do workdir falha.");

        var reportDirectory = Directory.GetDirectories(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "unwritable-reports")).Single();
        Assert.True(File.Exists(Path.Combine(reportDirectory, "summary.md")));
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
            new Dictionary<string, string>
            {
                ["ZAP_FAKE_SETFACL_RESULT"] = "success",
            });

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
        WriteFakeExecutable(Path.Combine(fakeBin, "chmod"), FakeChmodScript);
        WriteFakeExecutable(Path.Combine(fakeBin, "setfacl"), FakeSetfaclScript);
        var fakeExecutables = new[]
        {
            Path.Combine(fakeBin, "curl"),
            Path.Combine(fakeBin, "docker"),
            Path.Combine(fakeBin, "chmod"),
            Path.Combine(fakeBin, "setfacl"),
        };

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("set -e; ");
        commandBuilder.Append("chmod +x");
        foreach (var executable in fakeExecutables)
        {
            commandBuilder.Append(' ');
            commandBuilder.Append(ShellQuote(ToBashPath(executable)));
        }

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
        commandBuilder.Append("export ZAP_CHMOD_LOG=");
        commandBuilder.Append(ShellQuote($"./{_relativeRunRoot}/chmod.log"));
        commandBuilder.Append("; ");
        commandBuilder.Append("export ZAP_SETFACL_LOG=");
        commandBuilder.Append(ShellQuote($"./{_relativeRunRoot}/setfacl.log"));
        commandBuilder.Append("; ");
        commandBuilder.Append("export ZAP_COMMAND_RESOLUTION_LOG=");
        commandBuilder.Append(ShellQuote($"./{_relativeRunRoot}/command-resolution.log"));
        commandBuilder.Append("; ");
        commandBuilder.Append("for command_name in curl docker chmod setfacl; do printf '%s=%s\\n' \"$command_name\" \"$(command -v \"$command_name\")\" >> \"$ZAP_COMMAND_RESOLUTION_LOG\"; done; ");
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
        File.WriteAllText(path, content.ReplaceLineEndings("\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    private static string NormalizePathForComparison(string path)
    {
        if (OperatingSystem.IsWindows() && path.Length >= 3 && path[0] == '/' && path[2] == '/')
            path = char.ToUpperInvariant(path[1]) + ":" + path[2..];

        return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/').ToUpperInvariant();
    }

    private static void AssertExpectedZapArtifacts(string reportDirectory)
    {
        foreach (var fileName in new[]
        {
            "ledger-service-api.log",
            "ledger-service-api.html",
            "ledger-service-api.json",
            "ledger-service-api.md",
            "balance-service-api.log",
            "balance-service-api.html",
            "balance-service-api.json",
            "balance-service-api.md",
            "summary.md",
        })
        {
            Assert.True(File.Exists(Path.Combine(reportDirectory, fileName)), $"Artifact esperado nao encontrado: {fileName}");
        }
    }

    private void AssertFakeCommandsResolvedFromFakeBin()
    {
        var fakeBin = NormalizePathForComparison(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "fakebin"));
        var resolutionPath = Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "command-resolution.log");
        var resolutions = File.ReadAllLines(resolutionPath);

        foreach (var commandName in new[] { "curl", "docker", "chmod", "setfacl" })
        {
            var resolution = Assert.Single(resolutions, line => line.StartsWith($"{commandName}=", StringComparison.Ordinal));
            var commandPath = resolution[(commandName.Length + 1)..];
            Assert.StartsWith(fakeBin, NormalizePathForComparison(commandPath), StringComparison.Ordinal);
        }
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

    private const string FakeChmodScript = """
#!/usr/bin/env bash
set -euo pipefail

mode="${1:-}"
target="${2:-}"

if [[ -n "${ZAP_CHMOD_LOG:-}" && "$mode" =~ ^0?[0-9]{3,4}$ && -n "$target" ]]; then
  printf '%s|%s\n' "$mode" "$target" >> "$ZAP_CHMOD_LOG"
  if [[ "${ZAP_SKIP_CHMOD_MARKER:-}" != "true" ]]; then
    host_path="$target"
    if [[ "$host_path" =~ ^([A-Za-z]):\\(.*)$ ]]; then
      drive="${BASH_REMATCH[1],,}"
      rest="${BASH_REMATCH[2]//\\//}"
      host_path="/$drive/$rest"
    else
      host_path="${host_path//\\//}"
    fi
    if [[ -d "$host_path" ]]; then
      printf '%s\n' "$mode" > "$host_path/.fake-mode"
    fi
  fi
fi

exit 0
""";

    private const string FakeSetfaclScript = """
#!/usr/bin/env bash
set -euo pipefail

spec=""
target=""
previous=""

for arg in "$@"; do
  if [[ "$previous" == "-m" ]]; then
    spec="$arg"
  fi
  target="$arg"
  previous="$arg"
done

host_path="$target"
if [[ "$host_path" =~ ^([A-Za-z]):\\(.*)$ ]]; then
  drive="${BASH_REMATCH[1],,}"
  rest="${BASH_REMATCH[2]//\\//}"
  host_path="/$drive/$rest"
else
  host_path="${host_path//\\//}"
fi

if [[ -n "${ZAP_SETFACL_LOG:-}" ]]; then
  printf '%s|%s\n' "$spec" "$target" >> "$ZAP_SETFACL_LOG"
fi

if [[ "${ZAP_FAKE_SETFACL_RESULT:-failure}" != "success" ]]; then
  exit 1
fi

if [[ "$spec" =~ ^u:([0-9]+):(.+)$ && -d "$host_path" ]]; then
  printf '%s:%s\n' "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" > "$host_path/.fake-acl"
fi

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
    if [[ "$workdir" =~ ^([A-Za-z]):\\(.*)$ ]]; then
      drive="${BASH_REMATCH[1],,}"
      rest="${BASH_REMATCH[2]//\\//}"
      workdir="/$drive/$rest"
    else
      workdir="${workdir//\\//}"
    fi

    if [[ -z "$volume" && " $* " == *" --entrypoint sh "* ]]; then
      echo "uid=1000(zap) gid=1000(zap) groups=1000(zap)"
      echo "UID_GID=1000:1000"
      exit 0
    fi

    for arg in "$@"; do
      if [[ "$arg" == "sh" ]]; then
        mode=""
        acl=""
        if [[ -f "$workdir/.fake-mode" ]]; then
          mode="$(cat "$workdir/.fake-mode")"
        fi
        if [[ -f "$workdir/.fake-acl" ]]; then
          acl="$(cat "$workdir/.fake-acl")"
        fi

        if [[ "$acl" == "1000:rwx" || "$mode" == "0777" || "$mode" == "777" ]]; then
          exit 0
        fi

        echo "mktemp: failed to create file '/zap/wrk/.zap-write-test.XXXXXX': Permission denied" >&2
        exit 1
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
