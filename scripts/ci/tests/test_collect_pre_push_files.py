import os
import pathlib
import shutil
import stat
import subprocess
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
COLLECTOR = REPO_ROOT / "scripts" / "ci" / "collect-pre-push-files.py"
ZERO_SHA = "0000000000000000000000000000000000000000"


class CollectPrePushFilesTests(unittest.TestCase):
    def setUp(self) -> None:
        if shutil.which("git") is None:
            self.skipTest("git nao encontrado no PATH")

    def run_git(self, repo: pathlib.Path, *args: str) -> str:
        result = subprocess.run(
            ["git", *args],
            cwd=repo,
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(0, result.returncode, msg=f"git {' '.join(args)}\n{result.stderr}")
        return result.stdout.strip()

    def init_repo(self, path: pathlib.Path) -> None:
        self.run_git(path, "init", "-b", "main")
        self.run_git(path, "config", "user.email", "codex@example.test")
        self.run_git(path, "config", "user.name", "Codex")

    def write_file(self, repo: pathlib.Path, relative_path: str, content: str) -> None:
        file_path = repo / relative_path
        file_path.parent.mkdir(parents=True, exist_ok=True)
        file_path.write_text(content, encoding="utf-8")

    def commit_all(self, repo: pathlib.Path, message: str) -> str:
        self.run_git(repo, "add", "-A")
        self.run_git(repo, "commit", "-m", message)
        return self.run_git(repo, "rev-parse", "HEAD")

    def create_remote_main_ref(self, repo: pathlib.Path, sha: str) -> None:
        self.run_git(repo, "update-ref", "refs/remotes/origin/main", sha)
        self.run_git(repo, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main")

    def run_collector(
        self,
        repo: pathlib.Path,
        push_input: str,
        env: dict[str, str] | None = None,
        expected_returncode: int = 0,
    ) -> tuple[list[tuple[str, ...]], str, str]:
        output = repo / "collector-output.bin"
        collector_env = os.environ.copy()
        if env:
            collector_env.update(env)

        result = subprocess.run(
            ["python", str(COLLECTOR), "--output", str(output)],
            cwd=repo,
            input=push_input,
            text=True,
            capture_output=True,
            env=collector_env,
            check=False,
        )
        self.assertEqual(
            expected_returncode,
            result.returncode,
            msg=f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}",
        )

        records = self.read_records(output) if output.exists() else []
        return records, result.stdout, result.stderr

    def read_records(self, path: pathlib.Path) -> list[tuple[str, ...]]:
        data = path.read_bytes()
        if not data:
            return []

        tokens = data.split(b"\0")
        if tokens and tokens[-1] == b"":
            tokens.pop()

        records: list[tuple[str, ...]] = []
        index = 0
        while index < len(tokens):
            status = tokens[index].decode("utf-8")
            index += 1
            if status[0] in ("R", "C"):
                old_path = tokens[index].decode("utf-8")
                new_path = tokens[index + 1].decode("utf-8")
                index += 2
                records.append((status, old_path, new_path))
            else:
                file_path = tokens[index].decode("utf-8")
                index += 1
                records.append((status, file_path))

        return records

    def test_existing_branch_push_collects_only_committed_files(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "tracked.txt", "base\n")
            remote_sha = self.commit_all(repo, "base")

            self.write_file(repo, "tracked.txt", "changed\n")
            local_sha = self.commit_all(repo, "modify tracked")

            self.write_file(repo, "tracked.txt", "dirty unstaged\n")
            self.write_file(repo, "staged.txt", "staged but not committed\n")
            self.run_git(repo, "add", "staged.txt")
            self.write_file(repo, "untracked.txt", "untracked\n")

            records, _, _ = self.run_collector(
                repo,
                f"refs/heads/main {local_sha} refs/heads/main {remote_sha}\n",
            )

            self.assertEqual([("M", "tracked.txt")], records)

    def test_new_branch_first_push_uses_safe_base_ref_env(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "base.txt", "base\n")
            base_sha = self.commit_all(repo, "base")
            self.create_remote_main_ref(repo, base_sha)

            self.run_git(repo, "switch", "-c", "feature/new")
            self.write_file(repo, "feature.txt", "feature\n")
            local_sha = self.commit_all(repo, "feature")

            records, stdout, _ = self.run_collector(
                repo,
                f"refs/heads/feature/new {local_sha} refs/heads/feature/new {ZERO_SHA}\n",
                env={"PRE_PUSH_BASE_REF": "origin/main"},
            )

            self.assertIn("base identificada: origin/main", stdout)
            self.assertEqual([("A", "feature.txt")], records)

    def test_multiple_refs_are_accumulated_and_duplicate_records_are_removed(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "common.txt", "base\n")
            remote_sha = self.commit_all(repo, "base")

            self.run_git(repo, "switch", "-c", "feature/one")
            self.write_file(repo, "common.txt", "same change\n")
            first_sha = self.commit_all(repo, "first")

            self.run_git(repo, "switch", "main")
            self.run_git(repo, "switch", "-c", "feature/two")
            self.write_file(repo, "common.txt", "same change\n")
            self.write_file(repo, "other.txt", "other\n")
            second_sha = self.commit_all(repo, "second")

            records, _, _ = self.run_collector(
                repo,
                "\n".join(
                    [
                        f"refs/heads/feature/one {first_sha} refs/heads/feature/one {remote_sha}",
                        f"refs/heads/feature/two {second_sha} refs/heads/feature/two {remote_sha}",
                    ]
                )
                + "\n",
            )

            self.assertEqual([("M", "common.txt"), ("A", "other.txt")], records)

    def test_remote_branch_deletion_is_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "file.txt", "base\n")
            remote_sha = self.commit_all(repo, "base")

            records, stdout, _ = self.run_collector(
                repo,
                f"refs/heads/delete-me {ZERO_SHA} refs/heads/delete-me {remote_sha}\n",
            )

            self.assertIn("ref removida detectada", stdout)
            self.assertEqual([], records)

    def test_added_modified_deleted_renamed_and_copied_files_are_preserved(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "modified.txt", "base\n")
            self.write_file(repo, "deleted.txt", "delete me\n")
            self.write_file(repo, "renamed.txt", "rename me\n")
            self.write_file(repo, "copied.txt", "copy me\n")
            remote_sha = self.commit_all(repo, "base")

            self.write_file(repo, "added.txt", "new\n")
            self.write_file(repo, "modified.txt", "changed\n")
            (repo / "deleted.txt").unlink()
            self.run_git(repo, "mv", "renamed.txt", "renamed-new.txt")
            shutil.copyfile(repo / "copied.txt", repo / "copied-new.txt")
            local_sha = self.commit_all(repo, "mixed changes")

            records, _, _ = self.run_collector(
                repo,
                f"refs/heads/main {local_sha} refs/heads/main {remote_sha}\n",
            )

            self.assertIn(("A", "added.txt"), records)
            self.assertIn(("M", "modified.txt"), records)
            self.assertIn(("D", "deleted.txt"), records)
            self.assertIn(("R100", "renamed.txt", "renamed-new.txt"), records)
            self.assertIn(("C100", "copied.txt", "copied-new.txt"), records)

    def test_push_without_changes_produces_empty_output(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "file.txt", "base\n")
            sha = self.commit_all(repo, "base")

            records, _, _ = self.run_collector(
                repo,
                f"refs/heads/main {sha} refs/heads/main {sha}\n",
            )

            self.assertEqual([], records)

    def test_new_branch_without_safe_base_returns_clear_error(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "file.txt", "base\n")
            local_sha = self.commit_all(repo, "base")

            _, _, stderr = self.run_collector(
                repo,
                f"refs/heads/new {local_sha} refs/heads/new {ZERO_SHA}\n",
                expected_returncode=1,
            )

            self.assertIn("ref local: refs/heads/new", stderr)
            self.assertIn(f"SHA local: {local_sha}", stderr)
            self.assertIn("base tentada:", stderr)
            self.assertIn("motivo:", stderr)

    def test_paths_output_includes_old_and_new_paths_without_duplicates(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            repo = pathlib.Path(temp_dir)
            self.init_repo(repo)
            self.write_file(repo, "old.txt", "rename\n")
            remote_sha = self.commit_all(repo, "base")
            self.run_git(repo, "mv", "old.txt", "new.txt")
            local_sha = self.commit_all(repo, "rename")

            output = repo / "records.bin"
            paths_output = repo / "paths.txt"
            result = subprocess.run(
                [
                    "python",
                    str(COLLECTOR),
                    "--output",
                    str(output),
                    "--paths-output",
                    str(paths_output),
                ],
                cwd=repo,
                input=f"refs/heads/main {local_sha} refs/heads/main {remote_sha}\n",
                text=True,
                capture_output=True,
                check=False,
            )

            self.assertEqual(0, result.returncode, msg=result.stderr)
            self.assertEqual(["old.txt", "new.txt"], paths_output.read_text(encoding="utf-8").splitlines())


if __name__ == "__main__":
    unittest.main()
