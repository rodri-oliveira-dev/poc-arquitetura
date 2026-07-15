#!/usr/bin/env python3
import argparse
import fnmatch
import json
import pathlib
import sys

from sonar_context import load_contexts, normalize_path, repo_root
from path_security import resolve_existing_file


CONTEXTS = ["ledger", "balance", "transfer", "identity", "audit"]

CONTEXT_PATTERNS = {
    "ledger": ["src/ledger/**", "tests/ledger/**", "LedgerService.slnx", "tools/ComposeEnvGen/**"],
    "balance": ["src/balance/**", "tests/balance/**", "BalanceService.slnx"],
    "transfer": ["src/transfer/**", "tests/transfer/**", "TransferService.slnx"],
    "identity": ["src/identity/**", "tests/identity/**", "IdentityService.slnx"],
    "audit": ["src/audit/**", "tests/audit/**", "AuditService.slnx"],
}

GLOBAL_PATTERNS = [
    "global.json",
    "NuGet.config",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    ".editorconfig",
    ".globalconfig",
    "coverlet.runsettings",
    ".github/actions/setup-dotnet/**",
    ".github/workflows/dotnet.yml",
    ".github/workflows/sonarqube-context.yml",
    "scripts/quality/sonar-contexts.json",
    "scripts/quality/sonar_context.py",
    "scripts/quality/sonar_context_impact.py",
    "scripts/quality/sonarqube_cloud_report.py",
    "scripts/quality/sonarqube_context_summary.py",
]

EVENT_CONTRACT_PATTERNS = ["contracts/events/**"]
SHARED_PATTERNS = ["src/Shared/**", "tests/Shared/**", "PocArquitetura.Shared.slnx", "src/Shared/Directory.*"]
DOC_PATTERNS = ["docs/**", "*.md", "*.png", "*.jpg", "*.jpeg", "*.gif", "*.svg", "*.webp"]


def matches(path: str, patterns: list[str]) -> bool:
    return any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)


def load_files(path: pathlib.Path | None) -> list[str]:
    if path is None:
        lines = sys.stdin.read().splitlines()
    else:
        files_path = resolve_existing_file(
            path,
            repo_root(),
            base_dir=repo_root(),
            label="arquivo de changed files",
        )
        lines = files_path.read_text(encoding="utf-8").splitlines()

    return [line.strip().lstrip("\ufeff").lstrip("\xef\xbb\xbf").replace("\\", "/") for line in lines if line.strip()]


def all_contexts_selected() -> dict[str, bool]:
    return dict.fromkeys(CONTEXTS, True)


def no_contexts_selected() -> dict[str, bool]:
    return dict.fromkeys(CONTEXTS, False)


def resolve_push_contexts() -> tuple[dict[str, bool], bool, list[dict]]:
    return all_contexts_selected(), True, [{"path": "<push-main>", "reason": "main executa todos"}]


def resolve_dispatch_contexts(selected_context: str) -> tuple[dict[str, bool], bool, list[dict]]:
    if selected_context == "all":
        return all_contexts_selected(), True, [{"path": "<workflow_dispatch>", "reason": "all selecionado"}]

    if selected_context not in CONTEXTS:
        available = ", ".join(["all", *CONTEXTS])
        raise ValueError(f"Contexto invalido para workflow_dispatch: {selected_context}. Disponiveis: {available}")

    selected = no_contexts_selected()
    selected[selected_context] = True
    return selected, False, [{"path": "<workflow_dispatch>", "reason": f"{selected_context} selecionado"}]


def apply_changed_file_impact(changed_file: str, selected: dict[str, bool]) -> list[dict]:
    if matches(changed_file, DOC_PATTERNS):
        return [{"path": changed_file, "reason": "documentacao sem Sonar contextual"}]

    if matches(changed_file, GLOBAL_PATTERNS):
        selected.update(all_contexts_selected())
        return [{"path": changed_file, "reason": "global Sonar contextual"}]

    if matches(changed_file, EVENT_CONTRACT_PATTERNS):
        return [{"path": changed_file, "reason": "contrato de evento sem ownership Sonar contextual"}]

    if matches(changed_file, SHARED_PATTERNS):
        return [{"path": changed_file, "reason": "Shared sem Sonar contextual dedicado"}]

    reasons = []
    for context, patterns in CONTEXT_PATTERNS.items():
        if matches(changed_file, patterns):
            selected[context] = True
            reasons.append({"path": changed_file, "reason": f"{context} Sonar contextual"})

    return reasons or [{"path": changed_file, "reason": "sem impacto Sonar contextual conhecido"}]


def resolve_pull_request_contexts(files: list[str]) -> tuple[dict[str, bool], bool, list[dict]]:
    selected = no_contexts_selected()
    reasons = []

    for changed_file in files:
        reasons.extend(apply_changed_file_impact(changed_file, selected))

    return selected, all(selected.values()), reasons


def resolve_contexts(event_name: str, selected_context: str, files: list[str]) -> tuple[dict[str, bool], bool, list[dict]]:
    if event_name == "push":
        return resolve_push_contexts()
    if event_name == "workflow_dispatch":
        return resolve_dispatch_contexts(selected_context)
    return resolve_pull_request_contexts(files)


def build_matrix(selected: dict[str, bool], config_path: pathlib.Path) -> dict:
    contexts_config = load_contexts(config_path)
    include = []

    for context in CONTEXTS:
        if not selected[context]:
            continue

        config = contexts_config[context]
        include.append(
            {
                "context": context,
                "solution": normalize_path(config["solution"]),
                "project-key": config["projectKey"],
                "project-name": config["projectName"],
                "results-dir": normalize_path(config["resultsDir"]),
                "sonar-dir": normalize_path(config["sonarReportDir"]),
                "artifact-name": f"sonar-{context}",
            }
        )

    return {"include": include}


def print_github_output(payload: dict) -> None:
    for context in CONTEXTS:
        print(f"{context}={str(payload['contexts'][context]).lower()}")
    print(f"all={str(payload['all']).lower()}")
    print(f"has_contexts={str(payload['hasContexts']).lower()}")
    print(f"selected_contexts={payload['selectedContexts']}")
    print("matrix=" + json.dumps(payload["matrix"], separators=(",", ":")))
    print("summary_json=" + json.dumps(payload["summary"], separators=(",", ":")))


def main() -> int:
    parser = argparse.ArgumentParser(description="Detecta contextos Sonar impactados por uma lista de paths.")
    parser.add_argument("--event-name", required=True, choices=("pull_request", "push", "workflow_dispatch"))
    parser.add_argument("--selected-context", default="all")
    parser.add_argument("--files-from", type=pathlib.Path)
    parser.add_argument("--config", default=repo_root() / "scripts" / "quality" / "sonar-contexts.json", type=pathlib.Path)
    parser.add_argument("--format", choices=("json", "github-output"), default="json")
    args = parser.parse_args()

    try:
        files = load_files(args.files_from) if args.event_name == "pull_request" else []
        selected, all_contexts, reasons = resolve_contexts(args.event_name, args.selected_context, files)
        matrix = build_matrix(selected, args.config)
    except Exception as exc:
        print(exc, file=sys.stderr)
        return 2

    selected_contexts = [context for context in CONTEXTS if selected[context]]
    payload = {
        "contexts": selected,
        "all": all_contexts,
        "hasContexts": bool(selected_contexts),
        "selectedContexts": ",".join(selected_contexts),
        "matrix": matrix,
        "summary": {
            "selectedContexts": selected_contexts,
            "skippedContexts": [context for context in CONTEXTS if not selected[context]],
            "reasons": reasons,
        },
    }

    if args.format == "github-output":
        print_github_output(payload)
    else:
        print(json.dumps(payload, indent=2, sort_keys=True))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
