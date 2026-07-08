import pathlib
import unittest

import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from sonar_context_impact import CONTEXTS, resolve_contexts


class SonarContextImpactTests(unittest.TestCase):
    def assert_selected(self, payload: dict[str, bool], expected: set[str]) -> None:
        self.assertEqual(expected, {context for context, selected in payload.items() if selected})

    def test_push_selects_all_contexts(self) -> None:
        selected, all_contexts, _reasons = resolve_contexts("push", "all", [])
        self.assertTrue(all_contexts)
        self.assert_selected(selected, set(CONTEXTS))

    def test_workflow_dispatch_all_selects_all_contexts(self) -> None:
        selected, all_contexts, _reasons = resolve_contexts("workflow_dispatch", "all", [])
        self.assertTrue(all_contexts)
        self.assert_selected(selected, set(CONTEXTS))

    def test_workflow_dispatch_transfer_selects_transfer_only(self) -> None:
        selected, all_contexts, _reasons = resolve_contexts("workflow_dispatch", "transfer", [])
        self.assertFalse(all_contexts)
        self.assert_selected(selected, {"transfer"})

    def test_pull_request_ledger_path_selects_ledger(self) -> None:
        selected, _all_contexts, _reasons = resolve_contexts("pull_request", "all", ["src/ledger/SomeFile.cs"])
        self.assert_selected(selected, {"ledger"})

    def test_compose_env_gen_selects_ledger(self) -> None:
        selected, _all_contexts, _reasons = resolve_contexts("pull_request", "all", ["tools/ComposeEnvGen/Program.cs"])
        self.assert_selected(selected, {"ledger"})

    def test_global_path_selects_all_contexts(self) -> None:
        selected, all_contexts, _reasons = resolve_contexts("pull_request", "all", ["Directory.Packages.props"])
        self.assertTrue(all_contexts)
        self.assert_selected(selected, set(CONTEXTS))

    def test_docs_only_selects_no_contexts(self) -> None:
        selected, all_contexts, _reasons = resolve_contexts("pull_request", "all", ["docs/example.md"])
        self.assertFalse(all_contexts)
        self.assert_selected(selected, set())

    def test_event_contracts_keep_current_no_owner_decision(self) -> None:
        selected, all_contexts, reasons = resolve_contexts("pull_request", "all", ["contracts/events/example.schema.json"])
        self.assertFalse(all_contexts)
        self.assert_selected(selected, set())
        self.assertEqual("contrato de evento sem ownership Sonar contextual", reasons[0]["reason"])
