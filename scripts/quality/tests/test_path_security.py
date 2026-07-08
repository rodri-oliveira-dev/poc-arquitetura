import pathlib
import shutil
import tempfile
import unittest

import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from path_security import (
    PathSecurityError,
    resolve_existing_file,
    resolve_output_dir,
    resolve_output_file,
)


class PathSecurityTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = pathlib.Path(tempfile.mkdtemp())
        self.repo = self.temp_dir / "repo"
        self.allowed = self.repo / "artifacts"
        self.quality = self.repo / "scripts" / "quality"
        self.repo.mkdir()
        self.allowed.mkdir()
        self.quality.mkdir(parents=True)
        (self.repo / "changed-files.txt").write_text("src/ledger/SomeFile.cs\n", encoding="utf-8")
        (self.quality / "sonar-contexts.json").write_text("{}", encoding="utf-8")

    def tearDown(self) -> None:
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_accepts_valid_paths_inside_allowed_roots(self) -> None:
        artifact = resolve_output_dir("artifacts/sonarqube", self.allowed, base_dir=self.repo)
        config = resolve_existing_file(
            "scripts/quality/sonar-contexts.json",
            self.quality,
            base_dir=self.repo,
        )
        changed_files = resolve_existing_file("changed-files.txt", self.repo, base_dir=self.repo)

        self.assertEqual((self.allowed / "sonarqube").resolve(strict=False), artifact)
        self.assertEqual((self.quality / "sonar-contexts.json").resolve(strict=True), config)
        self.assertEqual((self.repo / "changed-files.txt").resolve(strict=True), changed_files)

    def test_rejects_traversal_outside_allowed_root(self) -> None:
        with self.assertRaisesRegex(PathSecurityError, "fora da raiz permitida"):
            resolve_output_file("artifacts/../../outside", self.allowed, base_dir=self.repo)

    def test_rejects_external_absolute_path(self) -> None:
        outside = self.temp_dir / "outside.json"
        with self.assertRaisesRegex(PathSecurityError, "fora da raiz permitida"):
            resolve_output_file(outside, self.allowed, base_dir=self.repo)

    def test_rejects_missing_required_input_file(self) -> None:
        with self.assertRaisesRegex(PathSecurityError, "inexistente"):
            resolve_existing_file("missing.txt", self.repo, base_dir=self.repo)

    def test_accepts_missing_output_inside_allowed_root(self) -> None:
        output = resolve_output_file("artifacts/new/result.json", self.allowed, base_dir=self.repo)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text("{}", encoding="utf-8")

        self.assertTrue(output.is_file())

    @unittest.skipIf(not hasattr(pathlib.Path, "symlink_to"), "symlink nao suportado neste ambiente")
    def test_rejects_symlink_escape(self) -> None:
        outside = self.temp_dir / "outside"
        outside.mkdir()
        link = self.allowed / "link"
        try:
            link.symlink_to(outside, target_is_directory=True)
        except OSError as exc:
            self.skipTest(f"symlink indisponivel neste ambiente: {exc}")

        with self.assertRaisesRegex(PathSecurityError, "fora da raiz permitida"):
            resolve_output_file("artifacts/link/result.json", self.allowed, base_dir=self.repo)
