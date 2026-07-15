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
ZERO_SHA = "0000000000000000000000000000000000000000"


class PrePushHookTests(unittest.TestCase):
    def setUp(self) -> None:
        self.sh = self.find_sh()
        if self.sh is None:
            self.skipTest("sh nao encontrado no PATH")

        self.real_python = shutil.which("python")
        if self.real_python is None:
            self.skipTest("python nao encontrado no PATH")

        self.real_dotnet = shutil.which("dotnet")
        if self.real_dotnet is None:
            self.skipTest("dotnet nao encontrado no PATH")

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
        records: list[tuple[str, ...]],
        *,
        push_input: str | None = None,
        restore_fail: str = "",
        build_fail: str = "",
        test_fail: str = "",
        compose_exit_code: int = 0,
        baseline_exit_code: int = 0,
        docker_compose_version_exit_code: int = 0,
        git_diff_exit_code: int = 0,
        expected_returncode: int = 0,
    ) -> tuple[str, str, list[str], list[str]]:
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = pathlib.Path(temp_dir)
            tools_path = temp_path / "tools"
            repo_path = temp_path / "repo"
            diff_output = temp_path / "git-diff.bin"
            dotnet_log = temp_path / "dotnet.log"
            container_log = temp_path / "container.log"
            tools_path.mkdir()
            repo_path.mkdir()

            self.copy_hook_dependencies(repo_path)
            self.create_solution_fixture(repo_path)
            diff_output.write_bytes(self.name_status_z(records))

            self.write_executable(
                tools_path / "git",
                f"""\
                #!/usr/bin/env sh
                set -eu

                if [ "$1" = "rev-parse" ] && [ "$2" = "--show-toplevel" ]; then
                  printf '%s\\n' "$PRE_PUSH_TEST_REPO_ROOT"
                  exit 0
                fi

                if [ "$1" = "diff" ] && [ "$2" = "-C" ] && [ "$3" = "--find-copies-harder" ] && [ "$4" = "--name-status" ] && [ "$5" = "-z" ]; then
                  if [ "${{PRE_PUSH_TEST_GIT_DIFF_EXIT_CODE:-0}}" -ne 0 ]; then
                    exit "$PRE_PUSH_TEST_GIT_DIFF_EXIT_CODE"
                  fi
                  cat "$PRE_PUSH_TEST_DIFF_OUTPUT"
                  exit 0
                fi

                echo "git stub recebeu comando inesperado: $*" >&2
                exit 1
                """,
            )
            self.write_windows_command_shim(tools_path / "git.cmd", tools_path / "git")

            for python_name in ("python", "python3"):
                self.write_executable(
                    tools_path / python_name,
                    f"""\
                    #!/usr/bin/env sh
                    set -eu
                    export PATH="$PRE_PUSH_TEST_WINDOWS_PATH"
                    exec {shlex.quote(self.to_sh_path(pathlib.Path(self.real_python)))} "$@"
                    """,
                )

            self.write_executable(
                tools_path / "dotnet",
                f"""\
                #!/usr/bin/env sh
                set -eu

                if [ "$1" = "run" ] && [ "$2" = "--file" ]; then
                  export PATH="$PRE_PUSH_TEST_WINDOWS_PATH"
                  exec {shlex.quote(self.to_sh_path(pathlib.Path(self.real_dotnet)))} "$@"
                fi

                first=true
                for argument in "$@"
                do
                  if [ "$first" = true ]; then
                    first=false
                  else
                    printf ' ' >>"{self.to_sh_path(dotnet_log)}"
                  fi
                  printf '%s' "$argument" >>"{self.to_sh_path(dotnet_log)}"
                done
                printf '\\n' >>"{self.to_sh_path(dotnet_log)}"

                if [ "$1" = "run" ] && [ "${{PRE_PUSH_TEST_BASELINE_EXIT_CODE:-0}}" -ne 0 ]; then
                  exit "$PRE_PUSH_TEST_BASELINE_EXIT_CODE"
                fi

                if [ "$1" = "restore" ] && [ "${{PRE_PUSH_TEST_RESTORE_FAIL:-}}" = "$2" ]; then
                  exit 17
                fi

                if [ "$1" = "build" ] && [ "${{PRE_PUSH_TEST_BUILD_FAIL:-}}" = "$2" ]; then
                  exit 23
                fi

                if [ "$1" = "test" ] && [ "${{PRE_PUSH_TEST_TEST_FAIL:-}}" = "$2" ]; then
                  exit 29
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
                  ./test.sh)
                    exit 0
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
            env["PRE_PUSH_TEST_DIFF_OUTPUT"] = self.to_sh_path(diff_output)
            env["PRE_PUSH_TEST_COMPOSE_EXIT_CODE"] = str(compose_exit_code)
            env["PRE_PUSH_TEST_BASELINE_EXIT_CODE"] = str(baseline_exit_code)
            env["PRE_PUSH_TEST_DOCKER_COMPOSE_VERSION_EXIT_CODE"] = str(docker_compose_version_exit_code)
            env["PRE_PUSH_TEST_GIT_DIFF_EXIT_CODE"] = str(git_diff_exit_code)
            env["PRE_PUSH_TEST_RESTORE_FAIL"] = restore_fail
            env["PRE_PUSH_TEST_BUILD_FAIL"] = build_fail
            env["PRE_PUSH_TEST_TEST_FAIL"] = test_fail
            env["PRE_PUSH_TEST_WINDOWS_PATH"] = f"{tools_path}{os.pathsep}{env.get('Path', env.get('PATH', ''))}"
            env["PRE_PUSH_GIT"] = str(tools_path / "git.cmd")

            command = (
                f"PATH={shlex.quote(self.to_sh_path(tools_path))}:$PATH "
                f"exec {shlex.quote(self.to_sh_path(PRE_PUSH_HOOK))}"
            )

            result = subprocess.run(
                [self.sh, "-c", command],
                input=push_input or self.existing_branch_input(),
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

            dotnet_commands = dotnet_log.read_text(encoding="utf-8").splitlines() if dotnet_log.exists() else []
            container_commands = container_log.read_text(encoding="utf-8").splitlines() if container_log.exists() else []
            return result.stdout, result.stderr, dotnet_commands, container_commands

    def copy_hook_dependencies(self, repo_path: pathlib.Path) -> None:
        (repo_path / "scripts" / "ci").mkdir(parents=True)
        (repo_path / "scripts" / "quality").mkdir(parents=True)
        shutil.copyfile(
            REPO_ROOT / "scripts" / "ci" / "collect-pre-push-files.py",
            repo_path / "scripts" / "ci" / "collect-pre-push-files.py",
        )
        shutil.copyfile(
            REPO_ROOT / "scripts" / "quality" / "resolve-solutions.cs",
            repo_path / "scripts" / "quality" / "resolve-solutions.cs",
        )

    def create_solution_fixture(self, repo_path: pathlib.Path) -> None:
        all_projects: list[str] = []
        for context in ("Audit", "Balance", "Identity", "Ledger", "Payment", "Transfer"):
            key = context.lower()
            projects = [
                f"src/{key}/{context}Service.Api/{context}Service.Api.csproj",
                f"src/{key}/{context}Service.Application/{context}Service.Application.csproj",
                f"tests/{key}/{context}Service.UnitTests/{context}Service.UnitTests.csproj",
            ]
            all_projects.extend(projects)
            self.write_slnx(repo_path, f"{context}Service.slnx", projects)

        shared_projects = [
            "src/Shared/ApiDefaults/ApiDefaults.csproj",
            "tests/Shared/ApiDefaults.Tests/ApiDefaults.Tests.csproj",
        ]
        self.write_slnx(repo_path, "PocArquitetura.Shared.slnx", shared_projects)
        self.write_slnx(
            repo_path,
            "PocArquitetura.slnx",
            [*all_projects, *shared_projects, "tests/Architecture.Tests/Architecture.Tests.csproj"],
        )

    def write_slnx(self, repo_path: pathlib.Path, file_name: str, projects: list[str]) -> None:
        xml = "\n".join(f'  <Project Path="{project}" />' for project in projects)
        (repo_path / file_name).write_text(f"<Solution>\n{xml}\n</Solution>\n", encoding="utf-8")

    def existing_branch_input(self) -> str:
        return (
            "refs/heads/test "
            "1111111111111111111111111111111111111111 "
            "refs/heads/test "
            "2222222222222222222222222222222222222222\n"
        )

    def new_branch_input(self) -> str:
        return (
            "refs/heads/test "
            "1111111111111111111111111111111111111111 "
            "refs/heads/test "
            f"{ZERO_SHA}\n"
        )

    def multiple_refs_input(self) -> str:
        return self.existing_branch_input() + (
            "refs/heads/other "
            "3333333333333333333333333333333333333333 "
            "refs/heads/other "
            "4444444444444444444444444444444444444444\n"
        )

    def name_status_z(self, records: list[tuple[str, ...]]) -> bytes:
        output = bytearray()
        for record in records:
            for field in record:
                output.extend(field.encode("utf-8"))
                output.append(0)
        return bytes(output)

    def to_sh_path(self, path: pathlib.Path) -> str:
        path_text = path.resolve().as_posix()
        if len(path_text) >= 3 and path_text[1:3] == ":/":
            return f"/{path_text[0].lower()}{path_text[2:]}"

        return path_text

    def write_executable(self, path: pathlib.Path, content: str) -> None:
        path.write_text(textwrap.dedent(content), encoding="utf-8", newline="\n")
        path.chmod(path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

    def write_windows_command_shim(self, path: pathlib.Path, shell_script: pathlib.Path) -> None:
        path.write_text(
            f'@"{self.sh}" "{shell_script}" %*\n',
            encoding="utf-8",
            newline="\r\n",
        )

    def assert_solution_flow(self, commands: list[str], solution: str) -> None:
        self.assertIn(f"restore ./{solution}", commands)
        self.assertIn(f"build ./{solution} --configuration Release --no-restore", commands)
        self.assertIn(
            f"test ./{solution} --configuration Release --no-build --no-restore --filter Category!=Integration&Category!=Container&Category!=Contract",
            commands,
        )

    def test_single_solution_impacted(self) -> None:
        stdout, _, commands, _ = self.run_pre_push([("M", "src/balance/BalanceService.Application/Foo.cs")])

        self.assertIn("Solutions impactadas:\n- BalanceService.slnx", stdout)
        self.assert_solution_flow(commands, "BalanceService.slnx")
        self.assertEqual(3, len(commands))

    def test_multiple_solutions_impacted(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [
                ("M", "src/balance/BalanceService.Application/Foo.cs"),
                ("M", "src/ledger/LedgerService.Application/Bar.cs"),
            ]
        )

        self.assertIn("- LedgerService.slnx", stdout)
        self.assertIn("- BalanceService.slnx", stdout)
        self.assert_solution_flow(commands, "LedgerService.slnx")
        self.assert_solution_flow(commands, "BalanceService.slnx")

    def test_multiple_files_same_solution_run_once(self) -> None:
        _, _, commands, _ = self.run_pre_push(
            [
                ("M", "src/balance/BalanceService.Application/Foo.cs"),
                ("M", "tests/balance/BalanceService.UnitTests/FooTests.cs"),
            ]
        )

        self.assertEqual(1, commands.count("restore ./BalanceService.slnx"))
        self.assertEqual(3, len(commands))

    def test_no_dotnet_solution_impacted(self) -> None:
        stdout, _, commands, _ = self.run_pre_push([("M", "docs/development/git-hooks.md")])

        self.assertIn("nenhuma alteracao localmente impactante detectada", stdout)
        self.assertEqual([], commands)

    def test_restore_failure_blocks_push_with_same_exit_code(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [("M", "src/balance/BalanceService.Application/Foo.cs")],
            restore_fail="./BalanceService.slnx",
            expected_returncode=17,
        )

        self.assertIn("pre-push bloqueado", stdout)
        self.assertIn("etapa: restore", stdout)
        self.assertIn("solution: BalanceService.slnx", stdout)
        self.assertIn("comando: dotnet restore ./BalanceService.slnx", stdout)
        self.assertEqual(["restore ./BalanceService.slnx"], commands)

    def test_build_failure_blocks_push_with_same_exit_code(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [("M", "src/balance/BalanceService.Application/Foo.cs")],
            build_fail="./BalanceService.slnx",
            expected_returncode=23,
        )

        self.assertIn("etapa: build", stdout)
        self.assertIn("solution: BalanceService.slnx", stdout)
        self.assertEqual(
            [
                "restore ./BalanceService.slnx",
                "build ./BalanceService.slnx --configuration Release --no-restore",
            ],
            commands,
        )

    def test_test_failure_blocks_push_with_same_exit_code(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [("M", "src/balance/BalanceService.Application/Foo.cs")],
            test_fail="./BalanceService.slnx",
            expected_returncode=29,
        )

        self.assertIn("etapa: test", stdout)
        self.assertIn("solution: BalanceService.slnx", stdout)
        self.assertEqual(3, len(commands))

    def test_stops_on_first_failure(self) -> None:
        _, _, commands, _ = self.run_pre_push(
            [
                ("M", "src/ledger/LedgerService.Application/Foo.cs"),
                ("M", "src/balance/BalanceService.Application/Bar.cs"),
            ],
            build_fail="./LedgerService.slnx",
            expected_returncode=23,
        )

        self.assertIn("build ./LedgerService.slnx --configuration Release --no-restore", commands)
        self.assertFalse(any("BalanceService.slnx" in command for command in commands))

    def test_zero_exit_allows_push(self) -> None:
        stdout, _, _, _ = self.run_pre_push([("M", "src/balance/BalanceService.Application/Foo.cs")])

        self.assertIn("pre-push: todas as validacoes foram aprovadas", stdout)

    def test_existing_branch(self) -> None:
        stdout, _, _, _ = self.run_pre_push([("M", "src/balance/BalanceService.Application/Foo.cs")])

        self.assertIn("branch remota existente detectada", stdout)

    def test_new_branch_without_safe_base_falls_back_conservatively(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [],
            push_input=self.new_branch_input(),
        )

        self.assertIn("validacoes serao executadas por seguranca", stdout)
        self.assert_solution_flow(commands, "PocArquitetura.Shared.slnx")
        self.assert_solution_flow(commands, "PocArquitetura.slnx")

    def test_multiple_refs(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [("M", "src/balance/BalanceService.Application/Foo.cs")],
            push_input=self.multiple_refs_input(),
        )

        self.assertEqual(2, stdout.count("branch remota existente detectada"))
        self.assert_solution_flow(commands, "BalanceService.slnx")

    def test_rename_between_contexts(self) -> None:
        stdout, _, commands, _ = self.run_pre_push(
            [("R100", "src/ledger/LedgerService.Application/Old.cs", "src/balance/BalanceService.Application/New.cs")]
        )

        self.assertIn("- LedgerService.slnx", stdout)
        self.assertIn("- BalanceService.slnx", stdout)
        self.assert_solution_flow(commands, "LedgerService.slnx")
        self.assert_solution_flow(commands, "BalanceService.slnx")

    def test_non_dotnet_validations_are_preserved(self) -> None:
        stdout, _, commands, container_commands = self.run_pre_push(
            [
                ("M", "compose.yaml"),
                ("M", "infra/terraform/environments/dev/main.tf"),
                ("M", "src/balance/BalanceService.Api/Dockerfile"),
            ]
        )

        self.assertIn("alteracao Docker Compose detectada em compose.yaml", stdout)
        self.assertIn("alteracao Terraform detectada em infra/terraform/environments/dev/main.tf", stdout)
        self.assertIn("alteracao de Dockerfile detectada em src/balance/BalanceService.Api/Dockerfile", stdout)
        self.assertIn("run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .", commands)
        self.assertIn("docker compose version", container_commands)
        self.assertIn("bash ./scripts/quality/containers/validate-compose-configs.sh", container_commands)
        self.assertIn("terraform fmt -check -recursive", "\n".join(container_commands))

    def test_unresolved_dotnet_path_uses_conservative_fallback(self) -> None:
        stdout, _, commands, _ = self.run_pre_push([("M", "src/newservice/NewService.Application/Foo.cs")])

        self.assertIn("resolvedor encontrou arquivo .NET sem solution segura", stdout)
        self.assert_solution_flow(commands, "PocArquitetura.Shared.slnx")
        self.assert_solution_flow(commands, "PocArquitetura.slnx")


if __name__ == "__main__":
    unittest.main()
