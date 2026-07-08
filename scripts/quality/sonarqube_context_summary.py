#!/usr/bin/env python3
import argparse
import json
import pathlib

from path_security import (
    artifacts_root,
    downloaded_sonar_artifacts_root,
    repo_root,
    resolve_existing_dir,
    resolve_output_file,
)


CONTEXTS = ["ledger", "balance", "transfer", "identity", "audit", "shared"]
METRICS = {
    "coverage": "Coverage",
    "bugs": "Bugs",
    "vulnerabilities": "Vulnerabilities",
    "code_smells": "Code Smells",
}


def load_json(path: pathlib.Path | None) -> dict | None:
    if not path or not path.exists():
        return None

    try:
        with path.open(encoding="utf-8") as file:
            return json.load(file)
    except Exception as exc:
        return {"error": f"{type(exc).__name__}: {exc}"}


def find_sonar_file(artifacts_dir: pathlib.Path, context: str, file_name: str) -> pathlib.Path | None:
    artifact_dir = artifacts_dir / f"sonar-{context}"
    if not artifact_dir.exists():
        return None

    matches = sorted(
        path
        for path in artifact_dir.rglob(file_name)
        if len(path.parts) >= 3 and path.parts[-3:-1] == ("sonarqube", context)
    )
    return matches[0] if matches else None


def metric_value(measures: dict | None, key: str) -> str:
    if not measures or measures.get("error"):
        return "UNAVAILABLE"

    for measure in measures.get("component", {}).get("measures", []):
        if measure.get("metric") == key:
            return str(measure.get("value", "UNAVAILABLE"))

    return "UNAVAILABLE"


def quality_gate_status(quality_gate: dict | None, expected: bool) -> str:
    if not expected:
        return "SKIPPED"
    if not quality_gate or quality_gate.get("error"):
        return "UNAVAILABLE"

    status = quality_gate.get("projectStatus", {}).get("status")
    if status == "OK":
        return "PASSED"
    if status in {"ERROR", "WARN"}:
        return "FAILED"

    return "UNAVAILABLE"


def markdown_table(headers: list[str], rows: list[list[str]]) -> str:
    lines = [
        "| " + " | ".join(headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(row) + " |")
    return "\n".join(lines) + "\n"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Consolida artifacts SonarQube contextuais.")
    parser.add_argument("--artifacts-dir", default="downloaded-sonar-artifacts", type=pathlib.Path)
    parser.add_argument("--selected-context", default="all")
    parser.add_argument("--expected-contexts", default="")
    parser.add_argument("--output", default="")
    return parser.parse_args()


def resolve_expected_contexts(selected_context: str, expected_contexts_arg: str) -> set[str]:
    if expected_contexts_arg:
        return {context.strip() for context in expected_contexts_arg.split(",") if context.strip()}
    return set(CONTEXTS if selected_context == "all" else [selected_context])


def build_context_rows(artifacts_dir: pathlib.Path, expected_contexts: set[str]) -> list[list[str]]:
    rows = []

    for context in CONTEXTS:
        expected = context in expected_contexts
        quality_gate = load_json(find_sonar_file(args.artifacts_dir, context, "quality-gate.json"))
        measures = load_json(find_sonar_file(args.artifacts_dir, context, "measures.json"))

        rows.append(
            [
                context.capitalize(),
                "RUN" if expected else "SKIPPED",
                quality_gate_status(quality_gate, expected),
                metric_value(measures, "coverage") if expected else "-",
                metric_value(measures, "bugs") if expected else "-",
                metric_value(measures, "vulnerabilities") if expected else "-",
                metric_value(measures, "code_smells") if expected else "-",
            ]
        )

    return rows


def build_report(rows: list[list[str]]) -> str:
    return "\n".join(
        [
            "## SonarQube contextual consolidado",
            "",
            markdown_table(["Contexto", "Execucao", "Quality Gate", *METRICS.values()], rows),
            "",
            "Estados: `PASSED` significa Quality Gate OK; `FAILED` significa Quality Gate remoto reprovado; "
            "`SKIPPED` significa contexto nao selecionado nesta execucao; `UNAVAILABLE` significa artifact ou API indisponivel.",
            "",
        ]
    )


def write_report(report: str, output: str) -> None:
    if not output:
        return

    output_path = resolve_output_file(
        output,
        artifacts_root(),
        base_dir=repo_root(),
        label="arquivo de saida do resumo Sonar contextual",
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(report, encoding="utf-8")


def main() -> int:
    args = parse_args()
    artifacts_dir = resolve_existing_dir(
        args.artifacts_dir,
        downloaded_sonar_artifacts_root(),
        base_dir=repo_root(),
        label="diretorio de artifacts Sonar contextual",
    )
    expected_contexts = resolve_expected_contexts(args.selected_context, args.expected_contexts)
    rows = build_context_rows(artifacts_dir, expected_contexts)
    report = build_report(rows)

    print(report)
    write_report(report, args.output)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
