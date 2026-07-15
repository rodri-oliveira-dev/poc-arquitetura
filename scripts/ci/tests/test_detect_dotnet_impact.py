import pathlib
import unittest

import importlib.util
import sys

SCRIPT_PATH = pathlib.Path(__file__).resolve().parents[1] / "detect-dotnet-impact.py"
spec = importlib.util.spec_from_file_location("detect_dotnet_impact", SCRIPT_PATH)
detect_dotnet_impact = importlib.util.module_from_spec(spec)
sys.modules["detect_dotnet_impact"] = detect_dotnet_impact
assert spec.loader is not None
spec.loader.exec_module(detect_dotnet_impact)

detect_impact = detect_dotnet_impact.detect_impact
parse_changed_paths = detect_dotnet_impact.parse_changed_paths


class DetectDotnetImpactTests(unittest.TestCase):
    def assert_impact(self, paths: list[str], aggregate: bool, shared: bool, docs_only: bool = False) -> None:
        result = detect_impact(paths)
        self.assertEqual(aggregate, result["run_aggregate"])
        self.assertEqual(shared, result["run_shared"])
        self.assertEqual(docs_only, result["docs_only"])

    def test_documentation_only(self) -> None:
        self.assert_impact(["docs/development/pull-request-validation.md", "docs/image.png"], False, False, True)

    def test_shared_source_change_runs_aggregate_and_shared(self) -> None:
        self.assert_impact(["src/Shared/Contracts/EventEnvelope.cs"], True, True)

    def test_shared_tests_change_runs_aggregate_and_shared(self) -> None:
        self.assert_impact(["tests/Shared/Contracts/EventEnvelopeTests.cs"], True, True)

    def test_shared_solution_change_runs_aggregate_and_shared(self) -> None:
        self.assert_impact(["PocArquitetura.Shared.slnx"], True, True)

    def test_each_service_change_runs_aggregate(self) -> None:
        for service in ("audit", "balance", "identity", "ledger", "payment", "transfer"):
            with self.subTest(service=service):
                self.assert_impact([f"src/{service}/Application/UseCase.cs"], True, False)

    def test_payment_solution_runs_aggregate(self) -> None:
        self.assert_impact(["PaymentService.slnx"], True, False)

    def test_tools_change_runs_aggregate(self) -> None:
        self.assert_impact(["tools/ComposeEnvGen/Program.cs"], True, False)

    def test_global_file_runs_both(self) -> None:
        self.assert_impact(["Directory.Build.targets"], True, True)

    def test_workflow_runs_both(self) -> None:
        result = detect_impact([".github/workflows/pull-request-validation.yml"])

        self.assertTrue(result["run_aggregate"])
        self.assertTrue(result["run_shared"])
        self.assertEqual("global", result["classification"][0]["reason"])

    def test_action_runs_both(self) -> None:
        result = detect_impact([".github/actions/setup-dotnet/action.yml"])

        self.assertTrue(result["run_aggregate"])
        self.assertTrue(result["run_shared"])
        self.assertEqual("global", result["classification"][0]["reason"])

    def test_normalize_path_preserves_dot_github_directory(self) -> None:
        paths = parse_changed_paths(".github/workflows/dotnet.yml", "lines")

        self.assertEqual([".github/workflows/dotnet.yml"], paths)

    def test_unknown_file_runs_both(self) -> None:
        self.assert_impact(["eng/new-build-input.txt"], True, True)

    def test_rename_between_directories_considers_previous_filename(self) -> None:
        raw = '[{"filename":"docs/payment.md","previous_filename":"src/payment/Application/PaymentService.cs"}]'
        paths = parse_changed_paths(raw, "json")
        self.assertEqual(["docs/payment.md", "src/payment/Application/PaymentService.cs"], paths)
        self.assert_impact(paths, True, False)

    def test_empty_list_falls_back_to_both(self) -> None:
        self.assert_impact([], True, True)

    def test_non_pull_request_fallback_runs_aggregate_and_shared(self) -> None:
        result = detect_dotnet_impact.fallback_result("push to main")

        self.assertTrue(result["run_aggregate"])
        self.assertTrue(result["run_shared"])
        self.assertFalse(result["docs_only"])

    def test_invalid_json_raises_for_callers_to_fallback(self) -> None:
        with self.assertRaises(ValueError):
            parse_changed_paths('{"filename":"src/ledger/File.cs"}', "json")

    def test_changed_csharp_files_are_reported(self) -> None:
        result = detect_impact(["src/ledger/File.cs", "docs/readme.md"])
        self.assertEqual(1, result["changed_csharp_count"])
        self.assertEqual(["src/ledger/File.cs"], result["changed_csharp_files"])


if __name__ == "__main__":
    unittest.main()
