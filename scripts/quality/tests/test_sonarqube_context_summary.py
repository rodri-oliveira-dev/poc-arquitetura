import json
import pathlib
import shutil
import tempfile
import unittest

import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from sonarqube_context_summary import build_context_rows, quality_gate_status


class SonarQubeContextSummaryTests(unittest.TestCase):
    def test_quality_gate_status_preserves_passed_failed_skipped_and_unavailable(self) -> None:
        self.assertEqual("PASSED", quality_gate_status({"projectStatus": {"status": "OK"}}, expected=True))
        self.assertEqual("FAILED", quality_gate_status({"projectStatus": {"status": "ERROR"}}, expected=True))
        self.assertEqual("SKIPPED", quality_gate_status({"projectStatus": {"status": "OK"}}, expected=False))
        self.assertEqual("UNAVAILABLE", quality_gate_status(None, expected=True))
        self.assertEqual("UNAVAILABLE", quality_gate_status({"error": "missing"}, expected=True))

    def test_build_context_rows_uses_received_artifacts_dir(self) -> None:
        temp_dir = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(lambda: shutil.rmtree(temp_dir, ignore_errors=True))
        sonar_dir = temp_dir / "sonar-ledger" / "sonarqube" / "ledger"
        sonar_dir.mkdir(parents=True)
        (sonar_dir / "quality-gate.json").write_text(
            json.dumps({"projectStatus": {"status": "OK"}}),
            encoding="utf-8",
        )
        (sonar_dir / "measures.json").write_text(
            json.dumps(
                {
                    "component": {
                        "measures": [
                            {"metric": "coverage", "value": "83.2"},
                            {"metric": "bugs", "value": "0"},
                            {"metric": "vulnerabilities", "value": "0"},
                            {"metric": "code_smells", "value": "5"},
                        ]
                    }
                }
            ),
            encoding="utf-8",
        )

        rows = build_context_rows(temp_dir, {"ledger"})

        self.assertEqual(["Ledger", "RUN", "PASSED", "83.2", "0", "0", "5"], rows[0])
