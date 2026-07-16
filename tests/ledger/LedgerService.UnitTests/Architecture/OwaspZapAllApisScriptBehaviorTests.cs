using System.Diagnostics;
using System.Text;

namespace LedgerService.UnitTests.Architecture;

public sealed class OwaspZapAllApisScriptBehaviorTests : IDisposable
{
    private readonly DirectoryInfo _repositoryRoot = GetRepositoryRoot();
    private readonly string _bashPath = ResolveBashPath();
    private readonly string _runId = Guid.NewGuid().ToString("N");
    private readonly string _relativeRunRoot;

    public OwaspZapAllApisScriptBehaviorTests()
    {
        _relativeRunRoot = $"artifacts/zap-all-apis-script-tests/{_runId}";
        Directory.CreateDirectory(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot));
    }

    [Fact]
    public void Scan_without_authentication_should_not_send_zap_auth_environment_variables()
    {
        var result = RunZapAllApisScript(
            [
                "--output-root", $"./{_relativeRunRoot}/reports",
                "--docker-network", "test-network",
                "--target", "LedgerService.Api|ledger-service-api|http://ledger-service:8080",
            ],
            new Dictionary<string, string>());

        Assert.Equal(0, result.ExitCode);

        var scanCommand = Assert.Single(File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "scan-commands.log")));
        Assert.Contains("AUTH_HEADER=false", scanCommand);
        Assert.Contains("AUTH_VALUE=false", scanCommand);
        Assert.DoesNotContain("ZAP_AUTH_HEADER", scanCommand);
        Assert.DoesNotContain("ZAP_AUTH_HEADER_VALUE", scanCommand);
        Assert.DoesNotContain("replacer.full_list", scanCommand);
        AssertPreservedZapApiScanArguments(scanCommand);
    }

    [Fact]
    public void Scan_with_manual_token_should_send_official_zap_auth_environment_variables_without_leaking_token()
    {
        const string token = "manual-token-that-must-not-leak";

        var result = RunZapAllApisScript(
            [
                "--output-root", $"./{_relativeRunRoot}/manual-token-reports",
                "--docker-network", "test-network",
                "--target", "LedgerService.Api|ledger-service-api|http://ledger-service:8080",
                "--token", token,
            ],
            new Dictionary<string, string>
            {
                ["EXPECTED_ZAP_TOKEN"] = token,
            });

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(token, result.StandardOutput);
        Assert.DoesNotContain(token, result.StandardError);

        var scanCommand = Assert.Single(File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "scan-commands.log")));
        Assert.Contains("AUTH_HEADER=true", scanCommand);
        Assert.Contains("AUTH_VALUE=true", scanCommand);
        Assert.Contains("AUTH_VALUE_MATCH=true", scanCommand);
        Assert.Contains("AUTH_ENV_BEFORE_IMAGE=true", scanCommand);
        Assert.DoesNotContain("replacer.full_list", scanCommand);
        AssertPreservedZapApiScanArguments(scanCommand);
        AssertTokenWasNotPersisted(token);
    }

    [Fact]
    public void Scan_with_authentication_should_obtain_token_with_get_token_script_and_send_official_zap_auth_environment_variables()
    {
        const string token = "obtained-token-that-must-not-leak";

        var result = RunZapAllApisScript(
            [
                "--output-root", $"./{_relativeRunRoot}/obtained-token-reports",
                "--docker-network", "test-network",
                "--target", "LedgerService.Api|ledger-service-api|http://ledger-service:8080",
                "--use-authentication",
                "--env-file", $"./{_relativeRunRoot}/token.env",
            ],
            new Dictionary<string, string>
            {
                ["EXPECTED_ZAP_TOKEN"] = token,
                ["FAKE_OIDC_TOKEN"] = token,
            });

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(token, result.StandardOutput);
        Assert.DoesNotContain(token, result.StandardError);

        var scanCommand = Assert.Single(File.ReadAllLines(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "scan-commands.log")));
        Assert.Contains("AUTH_HEADER=true", scanCommand);
        Assert.Contains("AUTH_VALUE=true", scanCommand);
        Assert.Contains("AUTH_VALUE_MATCH=true", scanCommand);
        Assert.Contains("AUTH_ENV_BEFORE_IMAGE=true", scanCommand);
        Assert.DoesNotContain("replacer.full_list", scanCommand);
        AssertPreservedZapApiScanArguments(scanCommand);
        AssertTokenWasNotPersisted(token);
    }

    public void Dispose()
    {
        var path = Path.Combine(_repositoryRoot.FullName, _relativeRunRoot);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private ProcessResult RunZapAllApisScript(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment)
    {
        var fakeBin = Path.Combine(_repositoryRoot.FullName, _relativeRunRoot, "fakebin");
        Directory.CreateDirectory(fakeBin);
        WriteFakeExecutable(Path.Combine(fakeBin, "docker"), FakeDockerScript);

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("set -e; ");
        commandBuilder.Append("chmod +x ");
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

        commandBuilder.Append("curl() { printf '{\"access_token\":\"%s\"}\\n200\\n' \"${FAKE_OIDC_TOKEN:?}\"; }; ");
        commandBuilder.Append("python3() { python \"$@\"; }; ");
        commandBuilder.Append("export -f curl python3; ");
        commandBuilder.Append("bash ./scripts/security/run-owasp-zap-all-apis.sh");
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
        startInfo.Environment["GITHUB_ACTIONS"] = "false";

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static void AssertPreservedZapApiScanArguments(string scanCommand)
    {
        Assert.Contains("-S", scanCommand);
        Assert.Contains("-I", scanCommand);
        Assert.Contains("-r ledger-service-api.html", scanCommand);
        Assert.Contains("-J ledger-service-api.json", scanCommand);
        Assert.Contains("-w ledger-service-api.md", scanCommand);
        Assert.Contains("-t http://ledger-service:8080/swagger/v1/swagger.json", scanCommand);
        Assert.Contains("-f openapi", scanCommand);
        Assert.Contains("-O http://ledger-service:8080", scanCommand);
    }

    private void AssertTokenWasNotPersisted(string token)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_repositoryRoot.FullName, _relativeRunRoot), "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain(token, content);
        }
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

    private const string FakeDockerScript = """
#!/usr/bin/env bash
set -euo pipefail

printf 'docker' >> "${ZAP_FAKE_LOG:?}"
for arg in "$@"; do
  if [[ "$arg" == ZAP_AUTH_HEADER_VALUE=Bearer* ]]; then
    printf ' %q' 'ZAP_AUTH_HEADER_VALUE=<redacted>' >> "$ZAP_FAKE_LOG"
  else
    printf ' %q' "$arg" >> "$ZAP_FAKE_LOG"
  fi
done
printf '\n' >> "$ZAP_FAKE_LOG"

case "${1:-}" in
  version)
    exit 0
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
    target=""
    format=""
    override=""
    html=""
    json=""
    markdown=""
    zap_options=""
    has_safe_mode="false"
    has_ignore_alerts="false"
    has_auth_header="false"
    has_auth_value="false"
    auth_value_match="false"
    auth_env_before_image="true"
    image_index=-1
    index=0
    previous=""

    for arg in "$@"; do
      if [[ "$arg" == "ghcr.io/zaproxy/zaproxy:stable" ]]; then
        image_index="$index"
      fi
      index=$((index + 1))
    done

    index=0
    for arg in "$@"; do
      case "$previous" in
        -v) volume="$arg" ;;
        -t) target="$arg" ;;
        -f) format="$arg" ;;
        -O) override="$arg" ;;
        -r) html="$arg" ;;
        -J) json="$arg" ;;
        -w) markdown="$arg" ;;
        -z) zap_options="$arg" ;;
      esac

      if [[ "$arg" == "ZAP_AUTH_HEADER=Authorization" ]]; then
        has_auth_header="true"
        if [[ "$image_index" -ge 0 && "$index" -gt "$image_index" ]]; then auth_env_before_image="false"; fi
      fi
      if [[ "$arg" == ZAP_AUTH_HEADER_VALUE=Bearer* ]]; then
        has_auth_value="true"
        if [[ "${EXPECTED_ZAP_TOKEN:-}" != "" && "$arg" == "ZAP_AUTH_HEADER_VALUE=Bearer $EXPECTED_ZAP_TOKEN" ]]; then
          auth_value_match="true"
        fi
        if [[ "$image_index" -ge 0 && "$index" -gt "$image_index" ]]; then auth_env_before_image="false"; fi
      fi
      if [[ "$arg" == "-S" ]]; then has_safe_mode="true"; fi
      if [[ "$arg" == "-I" ]]; then has_ignore_alerts="true"; fi

      previous="$arg"
      index=$((index + 1))
    done

    for arg in "$@"; do
      if [[ "$arg" == "python3" ]]; then
        echo "HTTP_STATUS=200"
        echo "OPERATION_COUNT=2"
        echo "GET /health"
        echo "POST /api/v1/lancamentos"
        exit 0
      fi
    done

    for arg in "$@"; do
      if [[ "$arg" == "zap-api-scan.py" ]]; then
        workdir="${volume%:/zap/wrk:rw}"
        if [[ "$workdir" =~ ^([A-Za-z]):\\(.*)$ ]]; then
          drive="${BASH_REMATCH[1],,}"
          rest="${BASH_REMATCH[2]//\\//}"
          workdir="/$drive/$rest"
        else
          workdir="${workdir//\\//}"
        fi

        printf 'zap-api-scan.py -t %s -f %s -O %s -r %s -J %s -w %s -z %s' "$target" "$format" "$override" "$html" "$json" "$markdown" "$zap_options" >> "${ZAP_SCAN_COMMANDS:?}"
        printf ' -S=%s -I=%s AUTH_HEADER=%s AUTH_VALUE=%s AUTH_VALUE_MATCH=%s AUTH_ENV_BEFORE_IMAGE=%s\n' "$has_safe_mode" "$has_ignore_alerts" "$has_auth_header" "$has_auth_value" "$auth_value_match" "$auth_env_before_image" >> "$ZAP_SCAN_COMMANDS"

        mkdir -p "$workdir"
        [[ -n "$html" ]] && printf '<html></html>\n' > "$workdir/$html"
        [[ -n "$json" ]] && printf '{}\n' > "$workdir/$json"
        [[ -n "$markdown" ]] && printf '# report\n' > "$workdir/$markdown"
        echo "stdout for $target"
        echo "stderr for $target" >&2
        exit 0
      fi
    done
    ;;
esac

echo "fake docker: unsupported command $*" >&2
exit 99
""";
}
