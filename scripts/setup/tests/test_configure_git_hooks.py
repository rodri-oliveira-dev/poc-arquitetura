import os
import pathlib
import shutil
import stat
import subprocess
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
BASH_SCRIPT = REPO_ROOT / "scripts" / "setup" / "configure-git-hooks.sh"
POWERSHELL_SCRIPT = REPO_ROOT / "scripts" / "setup" / "configure-git-hooks.ps1"
REQUIRED_HOOKS = ("commit-msg", "post-merge", "pre-push")


class ConfigureGitHooksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if shutil.which("git") is None:
            raise unittest.SkipTest("git nao encontrado no PATH")

        cls.sh = cls.find_sh()
        cls.powershell = shutil.which("pwsh") or shutil.which("powershell")

    @staticmethod
    def find_sh() -> str | None:
        candidates = [
            r"C:\Program Files\Git\bin\bash.exe",
            r"C:\Program Files\Git\usr\bin\bash.exe",
            r"C:\Program Files\Git\bin\sh.exe",
            r"C:\Program Files\Git\usr\bin\sh.exe",
            shutil.which("bash"),
            shutil.which("sh"),
        ]

        for candidate in candidates:
            if not candidate or not pathlib.Path(candidate).is_file():
                continue

            result = subprocess.run(
                [candidate, "-c", "exit 0"],
                text=True,
                capture_output=True,
                check=False,
                timeout=10,
            )
            if result.returncode == 0:
                return candidate

        return None

    def run_git(self, repo: pathlib.Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
        result = subprocess.run(
            ["git", *args],
            cwd=repo,
            text=True,
            capture_output=True,
            check=False,
        )
        if check and result.returncode != 0:
            self.fail(f"git {' '.join(args)} falhou\nstdout:\n{result.stdout}\nstderr:\n{result.stderr}")
        return result

    def init_repo(self, parent: pathlib.Path, name: str = "repo") -> pathlib.Path:
        repo = parent / name
        repo.mkdir(parents=True)
        self.run_git(repo, "init")
        self.create_hooks(repo)
        return repo

    def create_hooks(self, repo: pathlib.Path, hooks: tuple[str, ...] = REQUIRED_HOOKS) -> None:
        hooks_dir = repo / ".githooks"
        hooks_dir.mkdir(exist_ok=True)
        for hook in hooks:
            hook_path = hooks_dir / hook
            hook_path.write_text("#!/usr/bin/env sh\nexit 0\n", encoding="utf-8", newline="\n")
            hook_path.chmod(hook_path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

    def run_bash(self, cwd: pathlib.Path, *args: str) -> subprocess.CompletedProcess[str]:
        if self.sh is None:
            self.skipTest("bash/sh nao encontrado no PATH")

        return subprocess.run(
            [self.sh, str(BASH_SCRIPT), *args],
            cwd=cwd,
            text=True,
            capture_output=True,
            check=False,
        )

    def run_powershell(self, cwd: pathlib.Path, *args: str) -> subprocess.CompletedProcess[str]:
        if self.powershell is None:
            self.skipTest("PowerShell nao encontrado no PATH")

        return subprocess.run(
            [self.powershell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(POWERSHELL_SCRIPT), *args],
            cwd=cwd,
            text=True,
            capture_output=True,
            check=False,
        )

    def hooks_path(self, repo: pathlib.Path) -> str:
        result = self.run_git(repo, "config", "--local", "--get", "core.hooksPath", check=False)
        return result.stdout.strip() if result.returncode == 0 else ""

    def assert_success(self, result: subprocess.CompletedProcess[str]) -> None:
        self.assertEqual(0, result.returncode, msg=f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}")

    def assert_failure(self, result: subprocess.CompletedProcess[str]) -> None:
        self.assertNotEqual(0, result.returncode, msg=f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}")

    def test_bash_configures_repo_without_hooks_path(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))

            result = self.run_bash(repo)

            self.assert_success(result)
            self.assertEqual(".githooks", self.hooks_path(repo))
            self.assertIn("configurados com sucesso", result.stdout)

    def test_bash_already_configured_is_idempotent(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))
            self.run_git(repo, "config", "--local", "core.hooksPath", ".githooks")

            first = self.run_bash(repo)
            second = self.run_bash(repo)

            self.assert_success(first)
            self.assert_success(second)
            self.assertEqual(".githooks", self.hooks_path(repo))
            self.assertIn("ja estao configurados", second.stdout)

    def test_bash_refuses_to_overwrite_other_hooks_path(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))
            self.run_git(repo, "config", "--local", "core.hooksPath", "custom-hooks")

            result = self.run_bash(repo)

            self.assert_failure(result)
            self.assertEqual("custom-hooks", self.hooks_path(repo))
            self.assertIn("custom-hooks", result.stderr)
            self.assertIn("hooks pessoais, corporativos ou de outras ferramentas", result.stderr)

    def test_bash_force_overwrites_other_hooks_path(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))
            self.run_git(repo, "config", "--local", "core.hooksPath", "custom-hooks")

            result = self.run_bash(repo, "--force")

            self.assert_success(result)
            self.assertEqual(".githooks", self.hooks_path(repo))
            self.assertIn("Valor anterior: custom-hooks", result.stdout)
            self.assertIn("Novo valor: .githooks", result.stdout)

    def test_bash_check_only_validates_without_changes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))

            result = self.run_bash(repo, "--check")

            self.assert_failure(result)
            self.assertEqual("", self.hooks_path(repo))
            self.assertIn("nao esta configurado", result.stderr)

    def test_bash_fails_outside_git_repository(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            result = self.run_bash(pathlib.Path(temp))

            self.assert_failure(result)
            self.assertIn("nao esta dentro de um repositorio Git", result.stderr)

    def test_bash_fails_when_githooks_directory_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))
            shutil.rmtree(repo / ".githooks")

            result = self.run_bash(repo)

            self.assert_failure(result)
            self.assertEqual("", self.hooks_path(repo))
            self.assertIn("diretorio .githooks nao encontrado", result.stderr)

    def test_bash_fails_when_required_hook_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = self.init_repo(pathlib.Path(temp))
            (repo / ".githooks" / "pre-push").unlink()

            result = self.run_bash(repo)

            self.assert_failure(result)
            self.assertEqual("", self.hooks_path(repo))
            self.assertIn("pre-push", result.stderr)

    def test_bash_supports_repository_path_with_spaces(self) -> None:
        with tempfile.TemporaryDirectory(prefix="repo com espacos ") as temp:
            repo = self.init_repo(pathlib.Path(temp), name="meu repo")

            result = self.run_bash(repo)

            self.assert_success(result)
            self.assertEqual(".githooks", self.hooks_path(repo))

    def test_powershell_matches_bash_core_behaviour(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            base = pathlib.Path(temp)
            bash_repo = self.init_repo(base, "bash-repo")
            powershell_repo = self.init_repo(base, "powershell-repo")

            bash_result = self.run_bash(bash_repo)
            powershell_result = self.run_powershell(powershell_repo)

            self.assert_success(bash_result)
            self.assert_success(powershell_result)
            self.assertEqual(".githooks", self.hooks_path(bash_repo))
            self.assertEqual(".githooks", self.hooks_path(powershell_repo))

    def test_powershell_force_and_check_match_bash_statuses(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            base = pathlib.Path(temp)
            bash_repo = self.init_repo(base, "bash-repo")
            powershell_repo = self.init_repo(base, "powershell-repo")
            self.run_git(bash_repo, "config", "--local", "core.hooksPath", "custom-hooks")
            self.run_git(powershell_repo, "config", "--local", "core.hooksPath", "custom-hooks")

            bash_check = self.run_bash(bash_repo, "--check")
            powershell_check = self.run_powershell(powershell_repo, "-Check")
            bash_force = self.run_bash(bash_repo, "--force")
            powershell_force = self.run_powershell(powershell_repo, "-Force")

            self.assert_failure(bash_check)
            self.assert_failure(powershell_check)
            self.assert_success(bash_force)
            self.assert_success(powershell_force)
            self.assertEqual(".githooks", self.hooks_path(bash_repo))
            self.assertEqual(".githooks", self.hooks_path(powershell_repo))


if __name__ == "__main__":
    unittest.main()
