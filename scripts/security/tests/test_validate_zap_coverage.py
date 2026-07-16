import json
import pathlib
import subprocess
import sys
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
SCRIPT = REPO_ROOT / "scripts" / "security" / "validate-zap-coverage.py"


class ValidateZapCoverageTests(unittest.TestCase):
    def run_validator(self, root: pathlib.Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--reports-root",
                str(root),
                "--summary-output",
                str(root / "authenticated-coverage-summary.md"),
            ],
            cwd=REPO_ROOT,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )

    def write_report(
        self,
        root: pathlib.Path,
        name: str = "ledger-service-api.json",
        status: int = 200,
        path: str = "http://ledger-service:8080/api/v1/lancamentos",
        risk: str | None = None,
    ) -> pathlib.Path:
        payload: dict = {
            "messages": [
                {
                    "method": "GET",
                    "url": path,
                    "status": status,
                    "requestHeader": "Authorization: Bearer should-not-appear",
                    "responseBody": "client_secret=should-not-appear",
                }
            ],
            "site": [{"alerts": []}],
        }
        if risk is not None:
            payload["site"][0]["alerts"].append({"alert": f"{risk} alert", "riskdesc": risk})

        report = root / name
        report.write_text(json.dumps(payload), encoding="utf-8")
        (root / name.replace(".json", "-openapi-operations.txt")).write_text("GET /api/v1/lancamentos\n", encoding="utf-8")
        return report

    def test_report_without_401_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, status=200)

            result = self.run_validator(root)

            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("sucesso", result.stdout)

    def test_report_with_401_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, status=401)

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("HTTP 401", result.stdout)

    def test_report_with_403_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, status=403)

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("HTTP 403", result.stdout)

    def test_report_with_only_health_checks_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, path="http://ledger-service:8080/health")

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("nenhuma operacao de negocio", result.stdout)

    def test_report_without_business_operations_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, path="http://ledger-service:8080/swagger/v1/swagger.json")

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("nenhuma operacao de negocio", result.stdout)

    def test_missing_report_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            result = self.run_validator(pathlib.Path(directory))

            self.assertNotEqual(0, result.returncode)
            self.assertIn("nenhum relatorio JSON", result.stdout)

    def test_invalid_json_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "broken.json").write_text("{", encoding="utf-8")

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("JSON invalido", result.stdout)

    def test_multiple_apis_are_summarized(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, "ledger-service-api.json", 200)
            self.write_report(root, "balance-service-api.json", 404, "http://balance-service:8080/api/v1/consolidados")

            result = self.run_validator(root)

            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("ledger-service-api", result.stdout)
            self.assertIn("balance-service-api", result.stdout)

    def test_high_alert_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, risk="High")

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("alerta High", result.stdout)

    def test_medium_alert_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, risk="Medium")

            result = self.run_validator(root)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("alerta Medium", result.stdout)

    def test_low_alert_does_not_fail(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, risk="Low")

            result = self.run_validator(root)

            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("Low", result.stdout)

    def test_400_404_and_422_do_not_fail_authentication_gate(self) -> None:
        for status in (400, 404, 422):
            with self.subTest(status=status), tempfile.TemporaryDirectory() as directory:
                root = pathlib.Path(directory)
                self.write_report(root, status=status)

                result = self.run_validator(root)

                self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_zap_alert_instance_evidence_can_define_observed_status(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            payload = {
                "site": [
                    {
                        "alerts": [
                            {
                                "alert": "A Client Error response code was returned by the server",
                                "riskdesc": "Low",
                                "instances": [
                                    {
                                        "method": "GET",
                                        "uri": "http://ledger-service:8080/api/v1/lancamentos/missing",
                                        "evidence": "404",
                                    }
                                ],
                            }
                        ]
                    }
                ]
            }
            (root / "ledger-service-api.json").write_text(json.dumps(payload), encoding="utf-8")

            result = self.run_validator(root)

            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("'404': 1", result.stdout)

    def test_sensitive_data_is_not_printed(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.write_report(root, status=401, path="http://ledger-service:8080/api/v1/lancamentos?token=secret-token")

            result = self.run_validator(root)

            combined = result.stdout + result.stderr
            self.assertNotIn("secret-token", combined)
            self.assertNotIn("should-not-appear", combined)
            self.assertIn("token=%3Credacted%3E", combined)


if __name__ == "__main__":
    unittest.main()
