import os
import pathlib
import shlex
import shutil
import stat
import subprocess
import tempfile
import textwrap
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
PRE_PUSH_HOOK = REPO_ROOT / ".githooks" / "pre-push"


class PrePushPaymentTests(unittest.TestCase):
    def setUp(self) -> None:
        self.sh = self.find_sh()
        if self.sh is None:
            self.skipTest("sh nao encontrado no PATH")

    def find_sh(self) -> str | None:
        candidates = [
            shutil.which("sh"),
            r"C:\Program Files\Git\bin\sh.exe",
            r"C:\Program Files\Git\usr\bin\sh.exe",
            r"C:\Program Files (x86)\Git\bin\sh.exe",
            r"C:\Program Files (x86)\Git\usr\bin\sh.exe",
        ]

        for candidate in candidates:
            if candidate and pathlib.Path(candidate).is_file():
                return candidate

        return None

    def run_pre_push(
        self,
        changed_files: list[str],
        existing_files: list[str] | None = None,
        compose_exit_code: int = 0,
        baseline_exit_code: int = 0,
        docker_compose_version_exit_code: int = 0,
        git_diff_exit_code: int = 0,
        expected_returncode: int = 0,
    ) -> tuple[str, list[str]]:
        stdout, dotnet_commands, _ = self.run_pre_push_detailed(
            changed_files,
            existing_files,
            compose_exit_code,
            baseline_exit_code,
            docker_compose_version_exit_code,
            git_diff_exit_code,
            expected_returncode,
        )
        return stdout, dotnet_commands

    def run_pre_push_detailed(
        self,
        changed_files: list[str],
        existing_files: list[str] | None = None,
        compose_exit_code: int = 0,
        baseline_exit_code: int = 0,
        docker_compose_version_exit_code: int = 0,
        git_diff_exit_code: int = 0,
        expected_returncode: int = 0,
    ) -> tuple[str, list[str], list[str]]:
        existing_files = existing_files or []

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = pathlib.Path(temp_dir)
            tools_path = temp_path / "tools"
            repo_path = temp_path / "repo"
            dotnet_log = temp_path / "dotnet.log"
            container_log = temp_path / "container.log"
            tools_path.mkdir()
            repo_path.mkdir()

            for relative_file in existing_files:
                file_path = repo_path / relative_file
                file_path.parent.mkdir(parents=True, exist_ok=True)
                file_path.write_text("namespace PrePushPaymentTests;\n", encoding="utf-8")

            self.write_executable(
                tools_path / "git",
                f"""\
                #!/usr/bin/env sh
                set -eu

                if [ "$1" = "rev-parse" ] && [ "$2" = "--show-toplevel" ]; then
                  printf '%s\\n' "$PRE_PUSH_TEST_REPO_ROOT"
                  exit 0
                fi

                if [ "$1" = "diff" ] && [ "$2" = "--name-only" ]; then
                  if [ "${{PRE_PUSH_TEST_GIT_DIFF_EXIT_CODE:-0}}" -ne 0 ]; then
                    exit "$PRE_PUSH_TEST_GIT_DIFF_EXIT_CODE"
                  fi
                  printf '%s\\n' "$PRE_PUSH_TEST_CHANGED_FILES"
                  exit 0
                fi

                echo "git stub recebeu comando inesperado: $*" >&2
                exit 1
                """,
            )

            self.write_executable(
                tools_path / "dotnet",
                f"""\
                #!/usr/bin/env sh
                set -eu
                printf '%s\\n' "$*" >>"{self.to_sh_path(dotnet_log)}"
                if [ "$1" = "run" ] && [ "${{PRE_PUSH_TEST_BASELINE_EXIT_CODE:-0}}" -ne 0 ]; then
                  exit "$PRE_PUSH_TEST_BASELINE_EXIT_CODE"
                fi
                """,
            )

            self.write_executable(
                tools_path / "docker",
                f"""\
                #!/usr/bin/env sh
                set -eu
                printf 'docker %s\\n' "$*" >>"{self.to_sh_path(container_log)}"
                if [ "$1" = "compose" ] && [ "$2" = "version" ]; then
                  exit "$PRE_PUSH_TEST_DOCKER_COMPOSE_VERSION_EXIT_CODE"
                fi
                """,
            )

            self.write_executable(
                tools_path / "bash",
                f"""\
                #!/usr/bin/env sh
                set -eu
                printf 'bash %s\\n' "$*" >>"{self.to_sh_path(container_log)}"
                case "$1" in
                  ./scripts/quality/containers/validate-compose-configs.sh)
                    exit "$PRE_PUSH_TEST_COMPOSE_EXIT_CODE"
                    ;;
                esac
                echo "bash stub recebeu comando inesperado: $*" >&2
                exit 1
                """,
            )

            self.write_executable(
                tools_path / "terraform",
                f"""\
                #!/usr/bin/env sh
                set -eu
                printf 'terraform %s\\n' "$*" >>"{self.to_sh_path(container_log)}"
                """,
            )

            env = os.environ.copy()
            env["TMPDIR"] = self.to_sh_path(temp_path)
            env["PRE_PUSH_TEST_REPO_ROOT"] = self.to_sh_path(repo_path)
            env["PRE_PUSH_TEST_CHANGED_FILES"] = "\n".join(changed_files)
            env["PRE_PUSH_TEST_COMPOSE_EXIT_CODE"] = str(compose_exit_code)
            env["PRE_PUSH_TEST_BASELINE_EXIT_CODE"] = str(baseline_exit_code)
            env["PRE_PUSH_TEST_DOCKER_COMPOSE_VERSION_EXIT_CODE"] = str(docker_compose_version_exit_code)
            env["PRE_PUSH_TEST_GIT_DIFF_EXIT_CODE"] = str(git_diff_exit_code)
            command = (
                f"PATH={shlex.quote(self.to_sh_path(tools_path))}:$PATH "
                f"exec {shlex.quote(self.to_sh_path(PRE_PUSH_HOOK))}"
            )

            push_input = (
                "refs/heads/test "
                "1111111111111111111111111111111111111111 "
                "refs/heads/test "
                "2222222222222222222222222222222222222222\n"
            )
            result = subprocess.run(
                [self.sh, "-c", command],
                input=push_input,
                text=True,
                capture_output=True,
                env=env,
                cwd=repo_path,
                check=False,
            )

            self.assertEqual(
                expected_returncode,
                result.returncode,
                msg=f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}",
            )

            commands = dotnet_log.read_text(encoding="utf-8").splitlines() if dotnet_log.exists() else []
            container_commands = container_log.read_text(encoding="utf-8").splitlines() if container_log.exists() else []
            return result.stdout, commands, container_commands

    def to_sh_path(self, path: pathlib.Path) -> str:
        path_text = path.resolve().as_posix()
        if len(path_text) >= 3 and path_text[1:3] == ":/":
            return f"/{path_text[0].lower()}{path_text[2:]}"

        return path_text

    def write_executable(self, path: pathlib.Path, content: str) -> None:
        path.write_text(textwrap.dedent(content), encoding="utf-8", newline="\n")
        path.chmod(path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

    def assert_runs_payment_only(self, commands: list[str]) -> None:
        self.assertIn("restore ./PaymentService.slnx", commands)
        self.assertIn("build ./PaymentService.slnx --configuration Release --no-restore", commands)
        self.assertIn(
            "test ./PaymentService.slnx --configuration Release --no-build --no-restore --filter Category!=Integration&Category!=Container&Category!=Contract",
            commands,
        )

        unrelated_solutions = (
            "./AuditService.slnx",
            "./BalanceService.slnx",
            "./IdentityService.slnx",
            "./LedgerService.slnx",
            "./PocArquitetura.Shared.slnx",
            "./TransferService.slnx",
            "./PocArquitetura.slnx",
        )
        for command in commands:
            self.assertFalse(
                any(solution in command for solution in unrelated_solutions),
                msg=f"comando inesperado para outro contexto: {command}",
            )

    def assert_runs_conservative_fallback_once(
        self,
        stdout: str,
        dotnet_commands: list[str],
        container_commands: list[str],
    ) -> None:
        self.assertEqual(1, stdout.count("executando validacoes conservadoras"))
        self.assertIn("restore ./PocArquitetura.Shared.slnx", dotnet_commands)
        self.assertIn("restore ./PocArquitetura.slnx", dotnet_commands)
        self.assertIn("build ./PocArquitetura.Shared.slnx --configuration Release --no-restore", dotnet_commands)
        self.assertIn("build ./PocArquitetura.slnx --configuration Release --no-restore", dotnet_commands)
        self.assertIn(
            "test ./PocArquitetura.Shared.slnx --configuration Release --no-build --no-restore --filter Category!=Integration&Category!=Container&Category!=Contract",
            dotnet_commands,
        )
        self.assertIn(
            "test ./PocArquitetura.slnx --configuration Release --no-build --no-restore --filter Category!=Integration&Category!=Container&Category!=Contract",
            dotnet_commands,
        )
        self.assertIn("run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .", dotnet_commands)
        self.assertIn("docker compose version", container_commands)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)
        self.assertIn("terraform fmt -check -recursive", "\n".join(container_commands))
        self.assertFalse(any(command.startswith("format whitespace") for command in dotnet_commands))

    def assert_no_conservative_fallback(self, stdout: str) -> None:
        self.assertNotIn("arquivo sem classificacao de impacto", stdout)
        self.assertNotIn("executando validacoes conservadoras", stdout)

    def test_new_service_directory_runs_conservative_fallback(self) -> None:
        changed_file = "src/newservice/NewService.Api/Program.cs"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file], [changed_file])

        self.assertIn(f"arquivo sem classificacao de impacto: {changed_file}", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_new_root_configuration_runs_conservative_fallback(self) -> None:
        changed_file = "new-tooling.config"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"arquivo sem classificacao de impacto: {changed_file}", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_unknown_file_under_tools_runs_conservative_fallback(self) -> None:
        changed_file = "tools/NewTool/settings.custom"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"arquivo sem classificacao de impacto: {changed_file}", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_unknown_file_under_scripts_runs_conservative_fallback(self) -> None:
        changed_file = "scripts/new-tool.custom"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"arquivo sem classificacao de impacto: {changed_file}", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_unknown_file_mixed_with_markdown_runs_conservative_fallback(self) -> None:
        unknown_file = "eng/new-input.custom"
        markdown_file = "docs/development/git-hooks.md"
        stdout, commands, container_commands = self.run_pre_push_detailed([markdown_file, unknown_file])

        self.assertIn(f"alteracao documental ou nao impactante detectada em {markdown_file}", stdout)
        self.assertIn(f"arquivo sem classificacao de impacto: {unknown_file}", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_multiple_unknown_files_are_logged_and_fallback_runs_once(self) -> None:
        first_file = "eng/new-input.custom"
        second_file = "tools/NewTool/settings.custom"
        stdout, commands, container_commands = self.run_pre_push_detailed([first_file, second_file])

        self.assertIn(f"arquivo sem classificacao de impacto: {first_file}", stdout)
        self.assertIn(f"arquivo sem classificacao de impacto: {second_file}", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_unsafe_diff_uses_conservative_fallback(self) -> None:
        stdout, commands, container_commands = self.run_pre_push_detailed(
            [],
            git_diff_exit_code=1,
        )

        self.assertIn("validacoes serao executadas por seguranca", stdout)
        self.assert_runs_conservative_fallback_once(stdout, commands, container_commands)

    def test_ci_only_file_logs_without_local_validation(self) -> None:
        changed_file = ".github/workflows/infrastructure-security.yml"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"arquivo validado exclusivamente no CI reconhecido em {changed_file}", stdout)
        self.assertIn(f"nenhuma validacao local sera executada para {changed_file}", stdout)
        self.assertIn("gate correspondente permanece no Pull Request/GitHub Actions", stdout)
        self.assert_no_conservative_fallback(stdout)
        self.assertEqual([], commands)
        self.assertEqual([], container_commands)

    def test_src_payment_change_runs_payment(self) -> None:
        changed_file = "src/payment/PaymentService.Application/Foo.cs"
        stdout, commands = self.run_pre_push([changed_file], [changed_file])

        self.assertIn(f"alteracao Payment detectada em {changed_file}", stdout)
        self.assert_no_conservative_fallback(stdout)
        self.assert_runs_payment_only(commands)
        self.assertIn(
            f"format whitespace ./PaymentService.slnx --verify-no-changes --no-restore --verbosity minimal --include {changed_file}",
            commands,
        )

    def test_tests_payment_change_runs_payment(self) -> None:
        changed_file = "tests/payment/PaymentService.UnitTests/FooTests.cs"
        stdout, commands = self.run_pre_push([changed_file], [changed_file])

        self.assertIn(f"alteracao Payment detectada em {changed_file}", stdout)
        self.assert_runs_payment_only(commands)
        self.assertIn(
            f"format whitespace ./PaymentService.slnx --verify-no-changes --no-restore --verbosity minimal --include {changed_file}",
            commands,
        )

    def test_payment_solution_change_runs_payment(self) -> None:
        stdout, commands = self.run_pre_push(["PaymentService.slnx"])

        self.assertIn("alteracao Payment detectada em PaymentService.slnx", stdout)
        self.assert_runs_payment_only(commands)
        self.assertFalse(any(command.startswith("format whitespace ./PaymentService.slnx") for command in commands))

    def test_payment_only_does_not_run_unrelated_contexts(self) -> None:
        changed_file = "src/payment/PaymentService.Application/Foo.cs"
        _, commands = self.run_pre_push([changed_file], [changed_file])

        self.assert_runs_payment_only(commands)

    def test_global_change_with_payment_uses_global_strategy(self) -> None:
        changed_file = "src/payment/PaymentService.Application/Foo.cs"
        stdout, commands = self.run_pre_push(["Directory.Packages.props", changed_file], [changed_file])

        self.assertIn("alteracao Payment detectada", stdout)
        self.assertIn("alteracao .NET global substitui validacoes contextuais por Shared e agregadora", stdout)
        self.assertFalse(any("./PaymentService.slnx" in command for command in commands))
        self.assertIn("restore ./PocArquitetura.Shared.slnx", commands)
        self.assertIn("restore ./PocArquitetura.slnx", commands)
        self.assertIn(
            f"format whitespace ./PocArquitetura.slnx --verify-no-changes --no-restore --verbosity minimal --include {changed_file}",
            commands,
        )

    def test_deleted_csharp_file_is_not_sent_to_dotnet_format(self) -> None:
        deleted_file = "src/payment/PaymentService.Application/Deleted.cs"
        stdout, commands = self.run_pre_push([deleted_file])

        self.assertIn(f"alteracao Payment detectada em {deleted_file}", stdout)
        self.assert_runs_payment_only(commands)
        self.assertFalse(any(deleted_file in command for command in commands))
        self.assertFalse(any(command.startswith("format whitespace ./PaymentService.slnx") for command in commands))

    def test_payment_dockerfile_runs_container_baseline(self) -> None:
        changed_file = "src/payment/PaymentService.Api/Dockerfile"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"alteracao de Dockerfile detectada em {changed_file}", stdout)
        self.assert_no_conservative_fallback(stdout)
        self.assertIn("run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .", commands)
        self.assertEqual([], container_commands)

    def test_other_context_dockerfile_runs_container_baseline(self) -> None:
        changed_file = "src/ledger/LedgerService.Api/Dockerfile"
        stdout, commands, _ = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"alteracao de Dockerfile detectada em {changed_file}", stdout)
        self.assertIn("alteracao Ledger detectada", stdout)
        self.assertIn("run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .", commands)

    def test_compose_yaml_runs_compose_validation(self) -> None:
        stdout, commands, container_commands = self.run_pre_push_detailed(["compose.yaml"])

        self.assertIn("alteracao Docker Compose detectada em compose.yaml", stdout)
        self.assert_no_conservative_fallback(stdout)
        self.assertEqual([], commands)
        self.assertIn("docker compose version", container_commands)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)

    def test_terraform_known_path_runs_terraform_validation_without_fallback(self) -> None:
        changed_file = "infra/terraform/environments/dev/main.tf"
        stdout, commands, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"alteracao Terraform detectada em {changed_file}", stdout)
        self.assert_no_conservative_fallback(stdout)
        self.assertEqual([], commands)
        self.assertIn("terraform fmt -check -recursive", "\n".join(container_commands))

    def test_observability_compose_runs_compose_validation(self) -> None:
        stdout, _, container_commands = self.run_pre_push_detailed(["compose.observability.yaml"])

        self.assertIn("alteracao Docker Compose detectada em compose.observability.yaml", stdout)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)

    def test_subdirectory_compose_runs_compose_validation(self) -> None:
        changed_file = "infra/local/compose.test.yaml"
        stdout, _, container_commands = self.run_pre_push_detailed([changed_file])

        self.assertIn(f"alteracao Docker Compose detectada em {changed_file}", stdout)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)

    def test_docs_only_does_not_run_container_validation(self) -> None:
        stdout, commands, container_commands = self.run_pre_push_detailed(["docs/development/git-hooks.md"])

        self.assertIn("alteracao documental ou nao impactante detectada em docs/development/git-hooks.md", stdout)
        self.assert_no_conservative_fallback(stdout)
        self.assertIn("nenhuma alteracao localmente impactante detectada", stdout)
        self.assertEqual([], commands)
        self.assertEqual([], container_commands)

    def test_mixed_csharp_and_compose_runs_both_validation_sets(self) -> None:
        csharp_file = "src/payment/PaymentService.Application/Foo.cs"
        stdout, commands, container_commands = self.run_pre_push_detailed(["compose.yaml", csharp_file], [csharp_file])

        self.assertIn("alteracao Docker Compose detectada em compose.yaml", stdout)
        self.assertIn(f"alteracao Payment detectada em {csharp_file}", stdout)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)
        self.assert_runs_payment_only(commands)

    def test_multiple_dockerfiles_run_container_baseline_once(self) -> None:
        _, commands, _ = self.run_pre_push_detailed(
            [
                "src/payment/PaymentService.Api/Dockerfile",
                "src/ledger/LedgerService.Api/Dockerfile",
            ]
        )

        baseline_commands = [
            command
            for command in commands
            if command == "run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root ."
        ]
        self.assertEqual(1, len(baseline_commands))

    def test_invalid_compose_blocks_push(self) -> None:
        stdout, _, container_commands = self.run_pre_push_detailed(
            ["compose.yaml"],
            compose_exit_code=1,
            expected_returncode=1,
        )

        self.assertIn("configuracoes Docker Compose suportadas falhou", stdout)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)

    def test_container_baseline_violation_blocks_push(self) -> None:
        stdout, commands, _ = self.run_pre_push_detailed(
            ["src/payment/PaymentService.Api/Dockerfile"],
            baseline_exit_code=1,
            expected_returncode=1,
        )

        self.assertIn("baseline estatico de Dockerfiles e Compose falhou", stdout)
        self.assertIn("run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .", commands)

    def test_missing_docker_compose_warns_and_does_not_simulate_success(self) -> None:
        stdout, _, container_commands = self.run_pre_push_detailed(
            ["compose.yaml"],
            docker_compose_version_exit_code=1,
        )

        self.assertIn("Docker CLI com suporte a 'docker compose' nao encontrado", stdout)
        self.assertIn("validacao nao executada: scripts/quality/containers/validate-compose-configs.sh", stdout)
        self.assertIn("gate bloqueante de Compose continua no Pull Request/GitHub Actions", stdout)
        self.assertNotIn("configuracoes Docker Compose suportadas finalizado", stdout)
        self.assertIn("docker compose version", container_commands)
        self.assertNotIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)


if __name__ == "__main__":
    unittest.main()
