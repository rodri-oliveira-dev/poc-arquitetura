import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "enforce_coverage_threshold.py"


class EnforceCoverageThresholdTests(unittest.TestCase):
    def test_returns_success_when_line_coverage_equals_threshold(self):
        result = self.run_script(self.summary(linecoverage=80), "80")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("Line coverage: 80.0%", result.stdout)

    def test_returns_failure_when_line_coverage_is_below_threshold(self):
        result = self.run_script(self.summary(linecoverage=79.99), "80")

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Coverage below threshold (80.0%).", result.stderr)

    def test_returns_success_when_line_coverage_is_above_threshold(self):
        result = self.run_script(self.summary(linecoverage=80.01), "80")

        self.assertEqual(result.returncode, 0, result.stderr)

    def test_returns_failure_when_summary_file_is_missing(self):
        result = subprocess.run(
            [sys.executable, str(SCRIPT), "missing-summary.json", "80"],
            cwd=SCRIPT.parents[2],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Coverage summary not found", result.stderr)

    def test_returns_failure_when_required_assembly_is_below_threshold(self):
        result = self.run_script(
            self.summary(linecoverage=90, assembly_name="ApiDefaults", assembly_coverage=79.99),
            "80",
            "ApiDefaults",
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("ApiDefaults coverage below threshold (80.0%).", result.stderr)

    @staticmethod
    def summary(
        *,
        linecoverage: float,
        assembly_name: str = "ApiDefaults",
        assembly_coverage: float = 100,
    ) -> dict:
        return {
            "summary": {"linecoverage": linecoverage},
            "coverage": {
                "assemblies": [
                    {
                        "name": assembly_name,
                        "coverage": assembly_coverage,
                    }
                ]
            },
        }

    @staticmethod
    def run_script(payload: dict, threshold: str, required_assemblies: str = ""):
        with tempfile.TemporaryDirectory() as directory:
            summary = Path(directory) / "Summary.json"
            summary.write_text(json.dumps(payload), encoding="utf-8")
            command = [sys.executable, str(SCRIPT), str(summary), threshold]
            if required_assemblies:
                command.append(required_assemblies)

            return subprocess.run(
                command,
                cwd=SCRIPT.parents[2],
                check=False,
                capture_output=True,
                text=True,
            )


if __name__ == "__main__":
    unittest.main()
