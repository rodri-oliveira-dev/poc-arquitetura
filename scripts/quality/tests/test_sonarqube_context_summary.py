import pathlib
import unittest

import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from sonarqube_context_summary import quality_gate_status


class SonarQubeContextSummaryTests(unittest.TestCase):
    def test_quality_gate_status_preserves_passed_failed_skipped_and_unavailable(self) -> None:
        self.assertEqual("PASSED", quality_gate_status({"projectStatus": {"status": "OK"}}, expected=True))
        self.assertEqual("FAILED", quality_gate_status({"projectStatus": {"status": "ERROR"}}, expected=True))
        self.assertEqual("SKIPPED", quality_gate_status({"projectStatus": {"status": "OK"}}, expected=False))
        self.assertEqual("UNAVAILABLE", quality_gate_status(None, expected=True))
        self.assertEqual("UNAVAILABLE", quality_gate_status({"error": "missing"}, expected=True))
