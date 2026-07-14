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
    ) -> tuple[str, list[str]]:
        existing_files = existing_files or []

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = pathlib.Path(temp_dir)
            tools_path = temp_path / "tools"
            repo_path = temp_path / "repo"
            dotnet_log = temp_path / "dotnet.log"
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
                """,
            )

            env = os.environ.copy()
            env["TMPDIR"] = self.to_sh_path(temp_path)
            env["PRE_PUSH_TEST_REPO_ROOT"] = self.to_sh_path(repo_path)
            env["PRE_PUSH_TEST_CHANGED_FILES"] = "\n".join(changed_files)
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
                0,
                result.returncode,
                msg=f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}",
            )

            commands = dotnet_log.read_text(encoding="utf-8").splitlines() if dotnet_log.exists() else []
            return result.stdout, commands

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

    def test_src_payment_change_runs_payment(self) -> None:
        changed_file = "src/payment/PaymentService.Application/Foo.cs"
        stdout, commands = self.run_pre_push([changed_file], [changed_file])

        self.assertIn(f"alteracao Payment detectada em {changed_file}", stdout)
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


if __name__ == "__main__":
    unittest.main()
