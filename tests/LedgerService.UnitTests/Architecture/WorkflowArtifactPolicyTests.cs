using System.Text.RegularExpressions;

using YamlDotNet.RepresentationModel;

namespace LedgerService.UnitTests.Architecture;

public sealed partial class WorkflowArtifactPolicyTests
{
    private const string PlaceholderSecretLinePattern =
        @".*(PASSWORD|[Pp]assword|PWD|PGPASSWORD|CLIENT_SECRET|[Cc]lient[_-]?[Ss]ecret|SECRET|[Ss]ecret|TOKEN|[Tt]oken|API[_-]?KEY|[Aa]pi[_-]?[Kk]ey).*<[A-Z0-9_]*(PASSWORD|SECRET|TOKEN|API_KEY)[A-Z0-9_]*>.*";

    private static readonly string[] AllWorkflows =
    [
        ".github/workflows/codeql.yml",
        ".github/workflows/dependency-review.yml",
        ".github/workflows/dotnet.yml",
        ".github/workflows/mutation-tests.yml",
        ".github/workflows/pages-architecture.yml",
        ".github/workflows/pull-request-validation.yml",
        ".github/workflows/release.yml",
    ];

    private static readonly string[] WorkflowsWithPublishedArtifacts =
    [
        ".github/workflows/dotnet.yml",
        ".github/workflows/mutation-tests.yml",
    ];

    [Theory]
    [MemberData(nameof(GetAllWorkflows))]
    public void Workflow_should_have_valid_yaml_syntax(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));

        var yaml = new YamlStream();
        var act = () => yaml.Load(new StringReader(workflow));

        act();
    }

    [Theory]
    [MemberData(nameof(GetAllWorkflows))]
    public void Workflow_actions_should_be_pinned_to_full_commit_sha_with_original_version_comment(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));
        var actionReferences = ActionReferenceRegex().Matches(workflow).Cast<Match>().ToArray();
        Assert.NotEmpty(actionReferences);
        foreach (Match actionReference in actionReferences)
        {
            Assert.Matches("^[0-9a-f]{40}$", actionReference.Groups["ref"].Value);
            Assert.Matches(@"^v\d+", actionReference.Groups["comment"].Value);
        }
    }

    [Theory]
    [MemberData(nameof(GetWorkflowsWithPublishedArtifacts))]
    public void Upload_artifact_steps_should_have_explicit_retention_and_warn_on_missing_files(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));

        var uploadArtifactSteps = UploadArtifactStepRegex().Matches(workflow).Cast<Match>().ToArray();
        Assert.NotEmpty(uploadArtifactSteps);
        foreach (Match step in uploadArtifactSteps)
        {
            Assert.Contains("if-no-files-found: warn", step.Value);
            Assert.Matches(@"retention-days:\s*\d+", step.Value);
        }
    }

    [Fact]
    public void Dotnet_ci_should_not_publish_html_coverage_report_as_artifact()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));
        Assert.DoesNotContain("${{ env.TEST_RESULTS_DIR }}/coverage-report/**", workflow);
        Assert.Contains("${{ env.TEST_RESULTS_DIR }}/coverage-report/Summary.json", workflow);
        Assert.Contains("${{ env.TEST_RESULTS_DIR }}/coverage-report/Summary.txt", workflow);
    }

    [Fact]
    public void Dotnet_ci_should_suppress_only_placeholder_secret_lines_in_sonar()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));

        Assert.DoesNotContain("sonar.exclusions=\"**/appsettings.Local*.json\"", workflow);
        Assert.Contains("/d:sonar.issue.ignore.block=placeholderSecrets", workflow);
        Assert.Contains("/d:sonar.issue.ignore.block.placeholderSecrets.beginBlockRegexp=", workflow);
        Assert.Contains("/d:sonar.issue.ignore.block.placeholderSecrets.endBlockRegexp=", workflow);
        Assert.Contains(PlaceholderSecretLinePattern, workflow);

        var regex = PlaceholderSecretLineRegex();
        Assert.Matches(regex, "DefaultConnection=Host=127.0.0.1;Password=<LEDGER_DB_PASSWORD>");
        Assert.Matches(regex, "KEYCLOAK_CLIENT_SECRET=<KEYCLOAK_CLIENT_SECRET>");
        Assert.Matches(regex, "  -d \"password=<AUTH_POC_PASSWORD>\"");
        Assert.Matches(regex, "  --token \"<TOKEN>\"");
        Assert.Matches(regex, "ApiKey=<SOME_SECRET>");

        Assert.DoesNotMatch(regex, "DefaultConnection=Host=127.0.0.1;Password=postgres");
        Assert.DoesNotMatch(regex, "DefaultConnection=Host=127.0.0.1;Password=123456");
        Assert.DoesNotMatch(regex, "DefaultConnection=Host=127.0.0.1;Password=localpassword");
        Assert.DoesNotMatch(regex, "DefaultConnection=Host=127.0.0.1;Password=my-secret");
        Assert.DoesNotMatch(regex, "DefaultConnection=Host=127.0.0.1;Password=<local-secret>");
        Assert.DoesNotMatch(regex, "KEYCLOAK_BASE_URL=http://localhost:<KEYCLOAK_HOST_PORT>");
    }

    [Fact]
    public void Mutation_tests_should_publish_only_html_report_artifacts_with_short_retention()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/mutation-tests.yml"));
        Assert.Contains("tests/LedgerService.UnitTests/StrykerOutput/**/reports/mutation-report.html", workflow);
        Assert.Contains("tests/BalanceService.UnitTests/StrykerOutput/**/reports/mutation-report.html", workflow);
        Assert.False(OldLedgerStrykerOutputPathRegex().IsMatch(workflow));
        Assert.False(OldBalanceStrykerOutputPathRegex().IsMatch(workflow));
        Assert.True(RetentionDaysSevenRegex().Matches(workflow).Count >= 2);
    }

    public static TheoryData<string> GetWorkflowsWithPublishedArtifacts()
    {
        var data = new TheoryData<string>();

        foreach (var workflow in WorkflowsWithPublishedArtifacts)
            data.Add(workflow);

        return data;
    }

    public static TheoryData<string> GetAllWorkflows()
    {
        var data = new TheoryData<string>();

        foreach (var workflow in AllWorkflows)
            data.Add(workflow);

        return data;
    }

    [GeneratedRegex(@"uses:\s*actions/upload-artifact@[0-9a-f]{40}\s*#\s*v4[\s\S]*?(?=\n\s{6}- name:|\z)", RegexOptions.Multiline)]
    private static partial Regex UploadArtifactStepRegex();

    [GeneratedRegex(@"uses:\s+[\w.-]+/[\w.-]+(?:/[\w.-]+)?@(?<ref>[^\s#]+)\s*#\s*(?<comment>\S+)", RegexOptions.Multiline)]
    private static partial Regex ActionReferenceRegex();

    [GeneratedRegex(@"retention-days:\s*7")]
    private static partial Regex RetentionDaysSevenRegex();

    [GeneratedRegex(@"^\s*path:\s*tests/LedgerService\.UnitTests/StrykerOutput/\s*$", RegexOptions.Multiline)]
    private static partial Regex OldLedgerStrykerOutputPathRegex();

    [GeneratedRegex(@"^\s*path:\s*tests/BalanceService\.UnitTests/StrykerOutput/\s*$", RegexOptions.Multiline)]
    private static partial Regex OldBalanceStrykerOutputPathRegex();

    [GeneratedRegex(PlaceholderSecretLinePattern)]
    private static partial Regex PlaceholderSecretLineRegex();

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LedgerService.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!;
    }
}
