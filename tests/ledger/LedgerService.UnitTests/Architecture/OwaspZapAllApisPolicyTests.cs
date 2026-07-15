namespace LedgerService.UnitTests.Architecture;

public sealed class OwaspZapAllApisPolicyTests
{
    private static readonly string[] ApiNames =
    [
        "LedgerService.Api",
        "BalanceService.Api",
        "TransferService.Api",
        "PaymentService.Api",
        "AuditService.Api",
        "IdentityService.Api",
    ];

    private static readonly string[] ComposeServices =
    [
        "ledger-service",
        "balance-service",
        "transfer-service",
        "payment-service",
        "audit-service",
        "identity-service",
    ];

    [Fact]
    public void Workflow_should_scan_all_http_apis_directly_with_authentication()
    {
        DirectoryInfo repositoryRoot = GetRepositoryRoot();
        string workflow = File.ReadAllText(
            Path.Combine(repositoryRoot.FullName, ".github/workflows/owasp-zap.yml"));

        string[] hostHealthUrls =
        [
            "http://localhost:5226/health",
            "http://localhost:5228/health",
            "http://localhost:5230/health",
            "http://localhost:5234/health",
            "http://localhost:5235/health",
            "http://localhost:5232/health",
        ];

        string[] zapUrls =
        [
            "--ledger-zap-url http://ledger-service:8080",
            "--balance-zap-url http://balance-service:8080",
            "--transfer-zap-url http://transfer-service:8080",
            "--payment-zap-url http://payment-service:8080",
            "--audit-zap-url http://audit-service:8080",
            "--identity-zap-url http://identity-service:8080",
        ];

        foreach (string healthUrl in hostHealthUrls)
            Assert.Contains(healthUrl, workflow, StringComparison.Ordinal);

        foreach (string zapUrl in zapUrls)
            Assert.Contains(zapUrl, workflow, StringComparison.Ordinal);

        foreach (string composeService in ComposeServices)
            Assert.Contains(composeService, workflow, StringComparison.Ordinal);

        Assert.Contains("--use-authentication", workflow, StringComparison.Ordinal);
        Assert.Contains("ENV_FILE: ${{ github.workspace }}/.env.ci", workflow, StringComparison.Ordinal);
        Assert.Contains("compose.owasp-zap.yaml", workflow, StringComparison.Ordinal);
        Assert.Contains("bash ./scripts/security/run-owasp-zap-all-apis.sh", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gateway-service", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("continue-on-error", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("|| true", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Runner_should_validate_contract_coverage_and_preserve_operational_failures()
    {
        DirectoryInfo repositoryRoot = GetRepositoryRoot();
        string runner = File.ReadAllText(
            Path.Combine(repositoryRoot.FullName, "scripts/security/run-owasp-zap-all-apis.sh"));

        foreach (string apiName in ApiNames)
            Assert.Contains(apiName, runner, StringComparison.Ordinal);

        Assert.Contains("OPERATIONS=", runner, StringComparison.Ordinal);
        Assert.Contains("Documento OpenAPI nao contem operacoes HTTP", runner, StringComparison.Ordinal);
        Assert.Contains("APIs esperadas", runner, StringComparison.Ordinal);
        Assert.Contains("FINAL_EXIT_CODE", runner, StringComparison.Ordinal);
        Assert.Contains("if [[ \"$exit_code\" -ge 3 ]]", runner, StringComparison.Ordinal);
        Assert.Contains("setfacl -m \"u:${zap_uid}:rwx\" \"$OUTPUT_DIR\"", runner, StringComparison.Ordinal);
        Assert.Contains("chmod 0777 \"$OUTPUT_DIR\"", runner, StringComparison.Ordinal);
        Assert.Contains("chmod 0755 \"$OUTPUT_DIR\"", runner, StringComparison.Ordinal);
        Assert.Contains("OUTPUT_DIR=\"$(absolute_path \"$OUTPUT_DIR\")\"", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("--user root", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("--user 0", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("continue-on-error", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("|| true", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_overlay_should_only_enable_swagger_for_the_six_apis()
    {
        DirectoryInfo repositoryRoot = GetRepositoryRoot();
        string overlay = File.ReadAllText(
            Path.Combine(repositoryRoot.FullName, "compose.owasp-zap.yaml"));

        foreach (string composeService in ComposeServices)
        {
            Assert.Contains($"  {composeService}:", overlay, StringComparison.Ordinal);
        }

        Assert.Equal(ComposeServices.Length, CountOccurrences(overlay, "Swagger__Enabled: \"true\""));
        Assert.DoesNotContain("ports:", overlay, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0.0", overlay, StringComparison.Ordinal);
        Assert.DoesNotContain("gateway", overlay, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nginx", overlay, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int startIndex = 0;

        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private static DirectoryInfo GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory;
    }
}
