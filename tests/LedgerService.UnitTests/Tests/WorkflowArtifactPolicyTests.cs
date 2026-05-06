using System.Text.RegularExpressions;

using FluentAssertions;
using YamlDotNet.RepresentationModel;

namespace LedgerService.UnitTests.Tests;

public sealed partial class WorkflowArtifactPolicyTests
{
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

        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(GetAllWorkflows))]
    public void Workflow_actions_should_be_pinned_to_full_commit_sha_with_original_version_comment(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));
        var actionReferences = ActionReferenceRegex().Matches(workflow).Cast<Match>().ToArray();

        actionReferences.Should().NotBeEmpty();

        foreach (Match actionReference in actionReferences)
        {
            actionReference.Groups["ref"].Value.Should().MatchRegex("^[0-9a-f]{40}$");
            actionReference.Groups["comment"].Value.Should().MatchRegex(@"^v\d+");
        }
    }

    [Theory]
    [MemberData(nameof(GetWorkflowsWithPublishedArtifacts))]
    public void Upload_artifact_steps_should_have_explicit_retention_and_warn_on_missing_files(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));

        var uploadArtifactSteps = UploadArtifactStepRegex().Matches(workflow).Cast<Match>().ToArray();

        uploadArtifactSteps.Should().NotBeEmpty();

        foreach (Match step in uploadArtifactSteps)
        {
            step.Value.Should().Contain("if-no-files-found: warn");
            step.Value.Should().MatchRegex(@"retention-days:\s*\d+");
        }
    }

    [Fact]
    public void Dotnet_ci_should_not_publish_html_coverage_report_as_artifact()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));

        workflow.Should().NotContain("${{ env.TEST_RESULTS_DIR }}/coverage-report/**");
        workflow.Should().Contain("${{ env.TEST_RESULTS_DIR }}/coverage-report/Summary.json");
        workflow.Should().Contain("${{ env.TEST_RESULTS_DIR }}/coverage-report/Summary.txt");
    }

    [Fact]
    public void Mutation_tests_should_publish_only_html_report_artifacts_with_short_retention()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/mutation-tests.yml"));

        workflow.Should().Contain("tests/LedgerService.UnitTests/StrykerOutput/**/reports/mutation-report.html");
        workflow.Should().Contain("tests/BalanceService.UnitTests/StrykerOutput/**/reports/mutation-report.html");
        OldLedgerStrykerOutputPathRegex().IsMatch(workflow).Should().BeFalse();
        OldBalanceStrykerOutputPathRegex().IsMatch(workflow).Should().BeFalse();
        RetentionDaysSevenRegex().Matches(workflow).Should().HaveCountGreaterThanOrEqualTo(2);
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

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LedgerService.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the test must run inside the repository tree");
        return directory!;
    }
}
