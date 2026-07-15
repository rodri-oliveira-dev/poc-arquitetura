using System.Text.RegularExpressions;

using YamlDotNet.RepresentationModel;

namespace LedgerService.UnitTests.Architecture;

public sealed partial class WorkflowArtifactPolicyTests
{
    private const string PlaceholderSecretLinePattern =
        @".*(PASSWORD|[Pp]assword|PWD|PGPASSWORD|CLIENT_SECRET|[Cc]lient[_-]?[Ss]ecret|SECRET|[Ss]ecret|TOKEN|[Tt]oken|API[_-]?KEY|[Aa]pi[_-]?[Kk]ey).*<[A-Z0-9_]*(PASSWORD|SECRET|TOKEN|API_KEY)[A-Z0-9_]*>.*";

    private static readonly string[] AdvisoryWorkflowNames =
    [
        "pr-advisory-checks",
        "mutation-tests",
        "owasp-zap-baseline",
    ];

    private static readonly string[] PublishingWorkflowPaths =
    [
        ".github/workflows/pages-architecture.yml",
        ".github/workflows/publish-shared-nuget.yml",
        ".github/workflows/release.yml",
    ];

    [Theory]
    [MemberData(nameof(GetAllPolicyYamlFiles))]
    public void Workflow_and_composite_action_should_have_valid_yaml_syntax(string yamlPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var yamlText = File.ReadAllText(Path.Combine(repositoryRoot.FullName, yamlPath));

        var yaml = new YamlStream();
        void act()
        {
            yaml.Load(new StringReader(yamlText));
        }

        act();
    }

    [Theory]
    [MemberData(nameof(GetAllPolicyYamlFiles))]
    public void External_actions_should_be_pinned_to_full_commit_sha_with_original_version_comment(string yamlPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var yamlText = File.ReadAllText(Path.Combine(repositoryRoot.FullName, yamlPath));
        var actionReferences = ExternalActionReferenceRegex().Matches(yamlText).Cast<Match>().ToArray();

        foreach (var actionReference in actionReferences)
        {
            Assert.Matches("^[0-9a-f]{40}$", actionReference.Groups["ref"].Value);
            Assert.True(
                actionReference.Groups["comment"].Success,
                $"External action {actionReference.Groups["target"].Value} in {yamlPath} must keep a comment with the original semantic version.");
            Assert.Matches(@"^v\d+", actionReference.Groups["comment"].Value);
        }
    }

    [Theory]
    [MemberData(nameof(GetAllPolicyYamlFiles))]
    public void Workflows_and_composite_actions_should_not_use_latest_actions_or_images(string yamlPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var yamlText = File.ReadAllText(Path.Combine(repositoryRoot.FullName, yamlPath));

        Assert.DoesNotMatch(ActionLatestReferenceRegex(), yamlText);
        Assert.DoesNotMatch(DockerLatestReferenceRegex(), yamlText);
        Assert.DoesNotMatch(JobContainerLatestReferenceRegex(), yamlText);
    }

    [Theory]
    [MemberData(nameof(GetAllWorkflows))]
    public void Workflows_should_have_explicit_permissions(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var yaml = LoadYamlMapping(Path.Combine(repositoryRoot.FullName, workflowPath));

        if (TryGetChild(yaml, "permissions", out _))
            return;

        Assert.True(TryGetMapping(yaml, "jobs", out var jobs), $"Workflow {workflowPath} must declare top-level permissions or explicit permissions in every job.");
        foreach (var job in jobs.Children.Values.OfType<YamlMappingNode>())
        {
            Assert.True(HasKey(job, "permissions"), $"Workflow {workflowPath} has a job without explicit permissions.");
        }
    }

    [Theory]
    [MemberData(nameof(GetWorkflowsWithUploadArtifact))]
    public void Upload_artifact_steps_should_have_explicit_retention_and_if_no_files_found_policy(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));

        var uploadArtifactSteps = UploadArtifactStepRegex().Matches(workflow).Cast<Match>().ToArray();
        Assert.NotEmpty(uploadArtifactSteps);
        foreach (var step in uploadArtifactSteps)
        {
            Assert.Contains("if-no-files-found:", step.Value);
            Assert.Matches(@"retention-days:\s*\d+", step.Value);
        }
    }

    [Theory]
    [MemberData(nameof(GetPublishingWorkflows))]
    public void Publishing_workflows_should_not_cancel_in_progress_runs(string workflowPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflowPath));

        Assert.Contains("cancel-in-progress: false", workflow);
    }

    [Fact]
    public void Advisory_workflows_should_not_be_release_dependencies()
    {
        var repositoryRoot = GetRepositoryRoot();
        var releaseWorkflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/release.yml"));
        var publishWorkflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/publish-shared-nuget.yml"));

        foreach (var advisoryWorkflowName in AdvisoryWorkflowNames)
        {
            Assert.DoesNotContain(advisoryWorkflowName, releaseWorkflow);
            Assert.DoesNotContain(advisoryWorkflowName, publishWorkflow);
        }
    }

    [Fact]
    public void Script_quality_should_validate_workflows_with_actionlint_without_running_script_suite_for_workflow_only_changes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/script-quality.yml"));

        Assert.Contains("ACTIONLINT_VERSION:", workflow);
        Assert.Contains("ACTIONLINT_LINUX_AMD64_SHA256:", workflow);
        Assert.Contains("actionlint -color=false", workflow);
        Assert.Contains(".github/workflows/*|.github/actions/*)", workflow);
        Assert.Contains("scripts/*|package.json|package-lock.json)", workflow);
        Assert.Contains("needs.detect-impact.outputs.run_scripts == 'true'", workflow);
        Assert.Contains("needs.detect-impact.outputs.run_workflows == 'true'", workflow);
    }

    [Fact]
    public void Openapi_contract_validation_should_not_run_for_isolated_dockerfile_changes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/openapi-contracts.yml"));

        Assert.DoesNotContain("- \"src/**\"", workflow);
        Assert.Contains("- \"src/**/*Service.Api/**\"", workflow);
        Assert.Contains("- \"src/Shared/ApiDefaults/**\"", workflow);
        Assert.Contains("- \"!src/**/Dockerfile\"", workflow);
        Assert.Contains("- \"!src/**/*.Dockerfile\"", workflow);

        var apiPatternIndex = workflow.IndexOf("- \"src/**/*Service.Api/**\"", StringComparison.Ordinal);
        var apiDefaultsIndex = workflow.IndexOf("- \"src/Shared/ApiDefaults/**\"", StringComparison.Ordinal);
        var dockerfileExclusionIndex = workflow.IndexOf("- \"!src/**/Dockerfile\"", StringComparison.Ordinal);
        Assert.True(apiPatternIndex >= 0 && apiDefaultsIndex >= 0 && dockerfileExclusionIndex > apiPatternIndex && dockerfileExclusionIndex > apiDefaultsIndex);
    }

    [Fact]
    public void Pr_advisory_checks_should_ignore_edited_event_and_skip_draft_pull_requests()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/pr-advisory-checks.yml"));

        Assert.Contains("types: [opened, reopened, synchronize, ready_for_review]", workflow);
        Assert.DoesNotContain("edited", workflow);
        Assert.Contains("github.event.pull_request.draft == false", workflow);
        Assert.Contains("scripts/ci/detect-dotnet-impact.py", workflow);
    }

    [Fact]
    public void Dotnet_ci_should_not_publish_html_coverage_report_as_artifact()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));
        Assert.DoesNotContain("coverage-report/**", workflow);
        Assert.Contains("${{ env.ARTIFACTS_ROOT }}/test-results/**/coverage-report/Summary.json", workflow);
        Assert.Contains("${{ env.ARTIFACTS_ROOT }}/test-results/**/coverage-report/Summary.txt", workflow);
    }

    [Fact]
    public void Dotnet_ci_should_be_the_single_required_build_and_test_check()
    {
        var repositoryRoot = GetRepositoryRoot();
        var dotnetWorkflowPath = Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml");
        var workflow = File.ReadAllText(dotnetWorkflowPath);

        Assert.False(File.Exists(Path.Combine(repositoryRoot.FullName, ".github/workflows/pull-request-validation.yml")));
        Assert.False(File.Exists(Path.Combine(repositoryRoot.FullName, ".github/workflows/sonarqube-context.yml")));
        Assert.Contains("name: main-dotnet-ci", workflow);
        Assert.Contains("pull_request:", workflow);
        Assert.Contains("merge_group:", workflow);
        Assert.Contains("types: [checks_requested]", workflow);
        Assert.Contains("""branches: ["main"]""", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("name: Build and test", workflow);
        Assert.DoesNotContain("paths-ignore:", workflow);
    }

    [Fact]
    public void Dotnet_ci_should_use_centralized_impact_detection_for_context_selection()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));

        Assert.Contains("scripts/ci/detect-dotnet-impact.py", workflow);
        Assert.Contains("RUN_AGGREGATE: ${{ steps.changes.outputs.run_aggregate }}", workflow);
        Assert.Contains("RUN_SHARED: ${{ steps.changes.outputs.run_shared }}", workflow);
        Assert.Contains("PocArquitetura.slnx", workflow);
        Assert.Contains("PocArquitetura.Shared.slnx", workflow);
    }

    [Fact]
    public void Dotnet_ci_should_send_sonar_analysis_only_for_aggregate_context()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));

        Assert.Contains("aggregate|$AGGREGATE_SOLUTION_PATH|true|85|LedgerService.Worker,BalanceService.Worker|rodri-oliveira-dev_poc-arquitetura|poc-arquitetura", workflow);
        Assert.Contains("shared|$SHARED_SOLUTION_PATH|false|80|||", workflow);
        Assert.Contains("sonar_required=false", workflow);
        Assert.Contains("""if [ "$sonar_required" = "true" ] && [ "$sonar_allowed" = "true" ] && [ -z "${SONAR_TOKEN:-}" ]; then""", workflow);
        Assert.Contains("""if [ "$sonar_enabled" = "true" ] && [ "$sonar_allowed" = "true" ]; then""", workflow);
        Assert.Contains("""SONAR_REPORT_DIR="$sonar_report_dir" \""", workflow);
        Assert.Contains("""if: ${{ steps.changes.outputs.run_aggregate != 'true' && steps.changes.outputs.run_shared != 'true' }}""", workflow);
        Assert.Contains("""Restore, auditoria NuGet, SonarQube, build, testes e cobertura foram ignorados.""", workflow);
        Assert.Single(DotnetSonarscannerBeginRegex().Matches(workflow));
        Assert.Single(DotnetSonarscannerEndRegex().Matches(workflow));

        Assert.DoesNotContain("rodri-oliveira-dev_poc-arquitetura" + "-shared", workflow);
        Assert.DoesNotContain("poc-arquitetura" + "-shared", workflow);
        Assert.DoesNotContain("artifacts/sonarqube/" + "shared", workflow);
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
        Assert.Contains("tests/ledger/LedgerService.UnitTests/StrykerOutput/**/reports/mutation-report.html", workflow);
        Assert.Contains("tests/balance/BalanceService.UnitTests/StrykerOutput/**/reports/mutation-report.html", workflow);
        Assert.DoesNotMatch(OldLedgerStrykerOutputPathRegex(), workflow);
        Assert.DoesNotMatch(OldBalanceStrykerOutputPathRegex(), workflow);
        Assert.True(RetentionDaysSevenRegex().Count(workflow) >= 2);
    }

    [Fact]
    public void Owasp_zap_workflow_should_keep_host_health_checks_and_scan_through_compose_network()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/owasp-zap.yml"));

        Assert.Contains("http://localhost:5226/health", workflow);
        Assert.Contains("http://localhost:5228/health", workflow);
        Assert.Contains("docker inspect \"$ledger_container_id\"", workflow);
        Assert.Contains("awk '/(^|_)poc-net$/ { print; exit }'", workflow);
        Assert.Contains("grep -Fx \"$zap_network\"", workflow);
        Assert.Contains("--docker-network \"$zap_network\"", workflow);
        Assert.Contains("--ledger-zap-url http://ledger-service:8080", workflow);
        Assert.Contains("--balance-zap-url http://balance-service:8080", workflow);
        Assert.Contains("${{ env.ZAP_ARTIFACTS_DIR }}/**/*.log", workflow);
        Assert.DoesNotContain("continue-on-error", workflow);
        Assert.DoesNotContain("|| true", GetWorkflowStep(workflow, "Run OWASP ZAP baseline"));
    }

    [Fact]
    public void Owasp_zap_script_should_support_optional_network_and_separate_host_and_container_urls()
    {
        var repositoryRoot = GetRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "scripts/security/run-owasp-zap.sh"));

        Assert.Contains("--docker-network", script);
        Assert.Contains("--ledger-zap-url", script);
        Assert.Contains("--balance-zap-url", script);
        Assert.Contains("DOCKER_NETWORK=\"\"", script);
        Assert.Contains("LEDGER_ZAP_URL=\"\"", script);
        Assert.Contains("BALANCE_ZAP_URL=\"\"", script);
        Assert.Contains("assert_health \"LedgerService.Api\" \"$LEDGER_URL\"", script);
        Assert.Contains("assert_health \"BalanceService.Api\" \"$BALANCE_URL\"", script);
        Assert.Contains("assert_openapi_from_container \"LedgerService.Api\" \"$LEDGER_URL\" \"$LEDGER_ZAP_URL\"", script);
        Assert.Contains("assert_openapi_from_container \"BalanceService.Api\" \"$BALANCE_URL\" \"$BALANCE_ZAP_URL\"", script);
        Assert.Contains("run_zap_scan \"LedgerService.Api\" \"ledger-service-api\" \"$LEDGER_URL\" \"$LEDGER_ZAP_URL\"", script);
        Assert.Contains("run_zap_scan \"BalanceService.Api\" \"balance-service-api\" \"$BALANCE_URL\" \"$BALANCE_ZAP_URL\"", script);
        Assert.Contains("zap_accessible_base_url()", script);
        Assert.Contains("-O \"$effective_server_url\"", script);
    }

    [Fact]
    public void Owasp_zap_script_should_prepare_only_timestamped_workdir_for_non_root_container_user()
    {
        var repositoryRoot = GetRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "scripts/security/run-owasp-zap.sh"));

        Assert.Contains("OUTPUT_DIR=\"$(absolute_path \"$OUTPUT_DIR\")\"", script);
        Assert.Contains("prepare_zap_workdir()", script);
        Assert.Contains("detect_zap_image_identity", script);
        Assert.Contains("setfacl -m \"u:${zap_uid}:rwx\" \"$OUTPUT_DIR\"", script);
        Assert.Contains("chmod 0777 \"$OUTPUT_DIR\"", script);
        Assert.Contains("restore_zap_workdir_permissions", script);
        Assert.Contains("chmod 0755 \"$OUTPUT_DIR\"", script);
        Assert.Contains("prepare_zap_workdir", script);
        Assert.Contains("assert_zap_workdir_writable", script);
        Assert.True(script.IndexOf("prepare_zap_workdir", StringComparison.Ordinal) < script.IndexOf("assert_zap_workdir_writable", StringComparison.Ordinal));
        Assert.DoesNotContain("chmod -R 777", script);
        Assert.DoesNotContain("chmod -R 0777", script);
        Assert.DoesNotContain("--user root", script);
        Assert.DoesNotContain("--user 0", script);
    }

    [Fact]
    public void Owasp_zap_script_should_add_network_to_docker_run_only_when_informed()
    {
        var repositoryRoot = GetRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "scripts/security/run-owasp-zap.sh"));

        Assert.Contains("if [[ -n \"$DOCKER_NETWORK\" ]]; then", script);
        Assert.Contains("printf '%s\\n' \"--network\" \"$DOCKER_NETWORK\"", script);
        Assert.Contains("docker_args=(run --rm)", script);
        Assert.Contains("docker_args=(run --name \"$CONTAINER_NAME\" -v \"$OUTPUT_DIR:/zap/wrk:rw\")", script);
        Assert.Contains("done < <(docker_common_run_args)", script);
        Assert.DoesNotContain("--network poc-arquitetura_poc-net", script);
    }

    [Fact]
    public void Owasp_zap_script_should_preserve_legacy_localhost_gateway_behavior()
    {
        var repositoryRoot = GetRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "scripts/security/run-owasp-zap.sh"));

        Assert.Contains("LEDGER_ZAP_URL=\"$LEDGER_URL\"", script);
        Assert.Contains("BALANCE_ZAP_URL=\"$BALANCE_URL\"", script);
        Assert.Contains("host in (\"localhost\", \"127.0.0.1\", \"::1\")", script);
        Assert.Contains("netloc = \"host.docker.internal\"", script);
        Assert.Contains("local hosts=(\"host.docker.internal\")", script);
        Assert.Contains("if [[ \"$USE_NGINX\" == true ]]; then LEDGER_URL=\"https://ledger.localhost:7443\"; else LEDGER_URL=\"http://localhost:5226\"; fi", script);
        Assert.Contains("if [[ \"$USE_NGINX\" == true ]]; then BALANCE_URL=\"https://balance.localhost:7443\"; else BALANCE_URL=\"http://localhost:5228\"; fi", script);
    }

    [Fact]
    public void Owasp_zap_script_should_fail_operational_errors_and_invalid_openapi_imports()
    {
        var repositoryRoot = GetRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "scripts/security/run-owasp-zap.sh"));

        Assert.Contains("assert_docker_network", script);
        Assert.Contains("docker network inspect \"$DOCKER_NETWORK\"", script);
        Assert.Contains("Documento JSON nao contem campo 'openapi' ou 'swagger'.", script);
        Assert.Contains("Documento OpenAPI nao contem paths validos.", script);
        Assert.Contains("Campo 'servers' existe, mas nao e um array.", script);
        Assert.Contains("Servidor declarado no OpenAPI difere do servidor efetivo acessivel pelo container ZAP.", script);
        Assert.Contains("Falha ao validar OpenAPI do $api_name a partir do container ZAP.", script);
        Assert.Contains("URL vista pelo host: $host_openapi_url", script);
        Assert.Contains("URL vista pelo container: $target_url", script);
        Assert.Contains("Rede Docker utilizada: ${DOCKER_NETWORK:-<padrao do Docker>}", script);
        Assert.Contains("elif [[ \"$exit_code\" -ge 3 ]]; then", script);
        Assert.Contains("if [[ \"$exit_code\" -ge 3 ]]; then", script);
        Assert.Contains("if [[ \"$FAIL_ON_ALERTS\" == true && \"$exit_code\" -ne 0 ]]; then", script);
        Assert.Contains("final_exit_code()", script);
        Assert.Contains("Falha operacional: /zap/wrk nao esta gravavel pelo usuario da imagem ZAP.", script);
        Assert.Contains("Caminho absoluto montado: $OUTPUT_DIR", script);
        Assert.Contains("Ownership e permissoes no host: $host_stat", script);
        Assert.Contains("UID/GID usados pela imagem ZAP: $ZAP_IMAGE_IDENTITY", script);
        Assert.Contains("Saida completa da validacao: $output", script);
    }

    public static TheoryData<string> GetAllPolicyYamlFiles()
    {
        var data = new TheoryData<string>();

        foreach (var file in EnumeratePolicyYamlFiles())
            data.Add(file);

        return data;
    }

    public static TheoryData<string> GetAllWorkflows()
    {
        var data = new TheoryData<string>();

        foreach (var file in EnumerateWorkflowFiles())
            data.Add(file);

        return data;
    }

    public static TheoryData<string> GetWorkflowsWithUploadArtifact()
    {
        var data = new TheoryData<string>();
        var repositoryRoot = GetRepositoryRoot();

        foreach (var workflow in EnumerateWorkflowFiles())
        {
            var text = File.ReadAllText(Path.Combine(repositoryRoot.FullName, workflow));
            if (text.Contains("uses: actions/upload-artifact@", StringComparison.Ordinal))
                data.Add(workflow);
        }

        return data;
    }

    public static TheoryData<string> GetPublishingWorkflows()
    {
        var data = new TheoryData<string>();

        foreach (var workflow in PublishingWorkflowPaths)
            data.Add(workflow);

        return data;
    }

    private static IEnumerable<string> EnumeratePolicyYamlFiles()
    {
        var repositoryRoot = GetRepositoryRoot();
        return EnumerateWorkflowFiles()
            .Concat(
                Directory.EnumerateFiles(
                        Path.Combine(repositoryRoot.FullName, ".github", "actions"),
                        "action.yml",
                        SearchOption.AllDirectories)
                    .Select(path => ToRepositoryRelativePath(repositoryRoot, path)))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateWorkflowFiles()
    {
        var repositoryRoot = GetRepositoryRoot();
        return Directory.EnumerateFiles(Path.Combine(repositoryRoot.FullName, ".github", "workflows"), "*.yml")
            .Select(path => ToRepositoryRelativePath(repositoryRoot, path))
            .Order(StringComparer.Ordinal);
    }

    private static string ToRepositoryRelativePath(DirectoryInfo repositoryRoot, string path)
    {
        return Path.GetRelativePath(repositoryRoot.FullName, path).Replace('\\', '/');
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(File.ReadAllText(path)));
        Assert.Single(yaml.Documents);
        return Assert.IsType<YamlMappingNode>(yaml.Documents[0].RootNode);
    }

    private static bool TryGetMapping(YamlMappingNode node, string key, out YamlMappingNode mapping)
    {
        if (TryGetChild(node, key, out var child) && child is YamlMappingNode childMapping)
        {
            mapping = childMapping;
            return true;
        }

        mapping = null!;
        return false;
    }

    private static bool TryGetChild(YamlMappingNode node, string key, out YamlNode child)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                child = entry.Value;
                return true;
            }
        }

        child = null!;
        return false;
    }

    private static bool HasKey(YamlMappingNode node, string key)
    {
        return TryGetChild(node, key, out _);
    }

    private static string GetWorkflowStep(string workflow, string stepName)
    {
        var stepStart = workflow.IndexOf($"- name: {stepName}", StringComparison.Ordinal);
        Assert.True(stepStart >= 0, $"Step not found: {stepName}");

        var nextStepStart = workflow.IndexOf("\n      - name:", stepStart + 1, StringComparison.Ordinal);
        return nextStepStart < 0 ? workflow[stepStart..] : workflow[stepStart..nextStepStart];
    }

    [GeneratedRegex(@"uses:\s*actions/upload-artifact@[0-9a-f]{40}\s*#\s*v4[\s\S]*?(?=\n\s{6}- name:|\z)", RegexOptions.Multiline)]
    private static partial Regex UploadArtifactStepRegex();

    [GeneratedRegex(@"^\s*uses:\s+(?<target>(?!\.{1,2}/|docker://)[^\s@#]+)@(?<ref>[^\s#]+)(?:\s*#\s*(?<comment>\S+))?", RegexOptions.Multiline)]
    private static partial Regex ExternalActionReferenceRegex();

    [GeneratedRegex(@"^\s*uses:\s+(?!\.{1,2}/|docker://)[^\s@#]+@latest(?:\s|#|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ActionLatestReferenceRegex();

    [GeneratedRegex(@"docker://[^\s""']+:latest(?:\s|""|'|$)", RegexOptions.IgnoreCase)]
    private static partial Regex DockerLatestReferenceRegex();

    [GeneratedRegex(@"^\s*image:\s*[^\s#]+:latest(?:\s|#|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex JobContainerLatestReferenceRegex();

    [GeneratedRegex(@"retention-days:\s*7")]
    private static partial Regex RetentionDaysSevenRegex();

    [GeneratedRegex(@"^\s*path:\s*tests/ledger/LedgerService\.UnitTests/StrykerOutput/\s*$", RegexOptions.Multiline)]
    private static partial Regex OldLedgerStrykerOutputPathRegex();

    [GeneratedRegex(@"^\s*path:\s*tests/balance/BalanceService\.UnitTests/StrykerOutput/\s*$", RegexOptions.Multiline)]
    private static partial Regex OldBalanceStrykerOutputPathRegex();

    [GeneratedRegex(PlaceholderSecretLinePattern)]
    private static partial Regex PlaceholderSecretLineRegex();

    [GeneratedRegex("dotnet-sonarscanner begin")]
    private static partial Regex DotnetSonarscannerBeginRegex();

    [GeneratedRegex("dotnet-sonarscanner end")]
    private static partial Regex DotnetSonarscannerEndRegex();

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory;
    }
}
