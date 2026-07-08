import inspect
import json
import pathlib
import shutil
import subprocess
import tempfile
import unittest
from unittest import mock

import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

import sonarqube_cloud_report as report


class SonarQubeCloudReportSecurityTests(unittest.TestCase):
    def test_accepts_expected_sonarcloud_origin(self) -> None:
        self.assertEqual("https://sonarcloud.io", report.validate_sonarcloud_host_url("https://sonarcloud.io"))

    def test_rejects_untrusted_sonarcloud_origins(self) -> None:
        invalid_urls = [
            "http://sonarcloud.io",
            "https://sonarcloud.io.evil.example",
            "https://evil.example",
            "https://user:pass@sonarcloud.io",
            "file:///etc/passwd",
            "ftp://sonarcloud.io",
            "https://sonarcloud.io:8443",
            "https://sonarcloud.io?x=1",
            "https://sonarcloud.io/#fragment",
        ]

        for url in invalid_urls:
            with self.subTest(url=url):
                with self.assertRaises(ValueError):
                    report.validate_sonarcloud_host_url(url)

    def test_accepts_allowed_endpoint(self) -> None:
        self.assertEqual("measures/component", report.validate_api_endpoint("measures/component"))

    def test_rejects_arbitrary_endpoint(self) -> None:
        with self.assertRaisesRegex(ValueError, "nao permitido"):
            report.validate_api_endpoint("admin/users")

    @mock.patch("urllib.request.urlopen")
    def test_request_json_uses_allowed_url_query_auth_and_timeout(self, urlopen: mock.Mock) -> None:
        response = mock.MagicMock()
        response.read.return_value = b'{"ok": true}'
        urlopen.return_value.__enter__.return_value = response

        payload = report.request_json(
            "https://sonarcloud.io",
            "secret-token",
            "measures/component",
            {"component": "project:key", "metricKeys": "coverage,new_security_rating"},
        )

        self.assertEqual({"ok": True}, payload)
        request, = urlopen.call_args.args
        self.assertEqual(30, urlopen.call_args.kwargs["timeout"])
        parsed = report.urllib.parse.urlparse(request.full_url)
        query = report.urllib.parse.parse_qs(parsed.query)
        self.assertEqual("https", parsed.scheme)
        self.assertEqual("sonarcloud.io", parsed.hostname)
        self.assertEqual("/api/measures/component", parsed.path)
        self.assertEqual(["project:key"], query["component"])
        self.assertEqual(["coverage,new_security_rating"], query["metricKeys"])
        self.assertEqual("application/json", request.get_header("Accept"))
        self.assertTrue(request.get_header("Authorization").startswith("Basic "))
        self.assertNotIn("secret-token", request.full_url)
        self.assertNotIn("secret-token", str(request.headers))


class SonarQubeCloudReportMainTests(unittest.TestCase):
    def setUp(self) -> None:
        self.output_dir = report.artifacts_root() / "unit-sonarqube-report"
        shutil.rmtree(self.output_dir, ignore_errors=True)

    def tearDown(self) -> None:
        shutil.rmtree(self.output_dir, ignore_errors=True)

    def test_resolve_report_paths_accepts_fixed_artifacts_inside_allowed_root(self) -> None:
        paths = report.resolve_report_paths("artifacts/unit-sonarqube-report")

        self.assertEqual(self.output_dir, paths.output_dir)
        self.assertEqual(self.output_dir / "quality-gate.json", paths.quality_gate)
        self.assertEqual(self.output_dir / "measures.json", paths.measures)
        self.assertEqual(self.output_dir / "issues.json", paths.issues)
        for artifact_path in (
            paths.quality_gate,
            paths.measures,
            paths.issues,
        ):
            artifact_path.relative_to(self.output_dir)

    def test_resolve_report_paths_rejects_traversal_output_dir(self) -> None:
        with self.assertRaisesRegex(ValueError, "fora da raiz permitida"):
            report.resolve_report_paths("artifacts/../../outside")

    def test_resolve_report_paths_rejects_absolute_external_output_dir(self) -> None:
        outside = pathlib.Path(tempfile.gettempdir()) / "outside-sonar-report"
        with self.assertRaisesRegex(ValueError, "fora da raiz permitida"):
            report.resolve_report_paths(outside)

    def test_artifact_filenames_are_fixed_by_production_api(self) -> None:
        self.assertFalse(hasattr(report, "write_text_artifact"))
        self.assertFalse(hasattr(report, "write_json"))
        self.assertNotIn("report", report.SonarReportPaths.__dataclass_fields__)
        self.assertNotIn("report_alias", report.SonarReportPaths.__dataclass_fields__)
        self.assertEqual(["output_dir", "report"], list(inspect.signature(report.write_markdown_artifacts).parameters))

    def test_write_markdown_artifacts_accepts_artifacts_sonarqube_and_generates_only_fixed_markdown_files(self) -> None:
        output_dir = report.artifacts_root() / "sonarqube"
        shutil.rmtree(output_dir, ignore_errors=True)
        output_dir.mkdir(parents=True)
        try:
            report.write_markdown_artifacts("artifacts/sonarqube", "conteudo\n")

            markdown_files = sorted(path.name for path in output_dir.glob("*.md"))
            self.assertEqual(["report.md", "sonarqube-cloud-report.md"], markdown_files)
            self.assertEqual("conteudo\n", (output_dir / "sonarqube-cloud-report.md").read_text(encoding="utf-8"))
            self.assertEqual("conteudo\n", (output_dir / "report.md").read_text(encoding="utf-8"))
        finally:
            shutil.rmtree(output_dir, ignore_errors=True)

    def test_write_markdown_artifacts_rejects_traversal_output_dir(self) -> None:
        with self.assertRaisesRegex(ValueError, "fora da raiz permitida"):
            report.write_markdown_artifacts("artifacts/../../outside", "conteudo\n")

    def test_write_markdown_artifacts_rejects_absolute_external_output_dir(self) -> None:
        outside = pathlib.Path(tempfile.gettempdir()) / "outside-sonar-report"
        with self.assertRaisesRegex(ValueError, "fora da raiz permitida"):
            report.write_markdown_artifacts(outside, "conteudo\n")

    @unittest.skipIf(not hasattr(pathlib.Path, "symlink_to"), "symlink nao suportado neste ambiente")
    def test_write_markdown_artifacts_rejects_symlink_escape_inside_artifacts(self) -> None:
        outside = pathlib.Path(tempfile.mkdtemp())
        link = report.artifacts_root() / "unit-sonarqube-report-link"

        def remove_link() -> None:
            try:
                if link.is_symlink():
                    link.unlink()
                else:
                    link.rmdir()
            except FileNotFoundError:
                pass
            except NotADirectoryError:
                link.unlink(missing_ok=True)

        remove_link()

        try:
            link.symlink_to(outside, target_is_directory=True)
        except OSError as exc:
            if sys.platform != "win32":
                shutil.rmtree(outside, ignore_errors=True)
                self.skipTest(f"symlink indisponivel neste ambiente: {exc}")

            remove_link()
            junction = subprocess.run(
                ["cmd", "/c", "mklink", "/J", str(link), str(outside)],
                capture_output=True,
                text=True,
                check=False,
            )
            if junction.returncode != 0:
                shutil.rmtree(outside, ignore_errors=True)
                self.skipTest(f"symlink/junction indisponivel neste ambiente: {exc}")

        try:
            with self.assertRaisesRegex(ValueError, "fora da raiz permitida|nao pode ser resolvido"):
                report.write_markdown_artifacts(link, "conteudo\n")
        finally:
            remove_link()
            shutil.rmtree(outside, ignore_errors=True)

    @mock.patch.dict("os.environ", {"SONAR_TOKEN": "secret-token"}, clear=False)
    @mock.patch("sonarqube_cloud_report.request_json")
    def test_main_generates_artifacts_inside_allowed_root(self, request_json: mock.Mock) -> None:
        request_json.side_effect = [
            {"projectStatus": {"status": "OK", "conditions": []}},
            {"component": {"measures": [{"metric": "coverage", "value": "83.2"}]}},
            {"total": 0, "facets": [], "issues": []},
        ]

        result = report.main_with_args(
            [
                "--project-key",
                "project-key",
                "--organization-key",
                "org-key",
                "--output-dir",
                "artifacts/unit-sonarqube-report",
                "--pull-request",
                "65",
            ]
        )

        self.assertEqual(0, result)
        self.assertTrue((self.output_dir / "quality-gate.json").is_file())
        self.assertTrue((self.output_dir / "measures.json").is_file())
        self.assertTrue((self.output_dir / "issues.json").is_file())
        self.assertTrue((self.output_dir / "sonarqube-cloud-report.md").is_file())
        self.assertTrue((self.output_dir / "report.md").is_file())
        markdown = (self.output_dir / "report.md").read_text(encoding="utf-8")
        self.assertIn("pullRequest=65", markdown)
        self.assertNotIn("secret-token", markdown)

    @mock.patch.dict("os.environ", {"SONAR_TOKEN": "secret-token"}, clear=False)
    @mock.patch("sonarqube_cloud_report.request_json", side_effect=OSError("network down"))
    def test_main_writes_error_report_when_network_fails(self, _request_json: mock.Mock) -> None:
        result = report.main_with_args(
            [
                "--project-key",
                "project-key",
                "--organization-key",
                "org-key",
                "--output-dir",
                "artifacts/unit-sonarqube-report",
            ]
        )

        self.assertEqual(0, result)
        quality_gate = json.loads((self.output_dir / "quality-gate.json").read_text(encoding="utf-8"))
        markdown = (self.output_dir / "report.md").read_text(encoding="utf-8")
        self.assertIn("OSError: network down", quality_gate["error"])
        self.assertIn("Relatorio SonarQube Cloud nao gerado", markdown)

    def test_main_rejects_output_outside_artifacts_root(self) -> None:
        outside = pathlib.Path(tempfile.gettempdir()) / "outside-sonar-report"
        result = report.main_with_args(
            [
                "--project-key",
                "project-key",
                "--organization-key",
                "org-key",
                "--output-dir",
                str(outside),
            ]
        )

        self.assertEqual(2, result)
