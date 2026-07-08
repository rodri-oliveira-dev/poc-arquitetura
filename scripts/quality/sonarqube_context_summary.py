#!/usr/bin/env python3
import argparse
import json
import pathlib


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


def main() -> int:
    parser = argparse.ArgumentParser(description="Consolida artifacts SonarQube contextuais.")
    parser.add_argument("--artifacts-dir", default="downloaded-sonar-artifacts", type=pathlib.Path)
    parser.add_argument("--selected-context", default="all")
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    expected_contexts = set(CONTEXTS if args.selected_context == "all" else [args.selected_context])
    rows = []

    for context in CONTEXTS:
        expected = context in expected_contexts
        quality_gate = load_json(find_sonar_file(args.artifacts_dir, context, "quality-gate.json"))
        measures = load_json(find_sonar_file(args.artifacts_dir, context, "measures.json"))

        rows.append(
            [
                context.capitalize(),
                quality_gate_status(quality_gate, expected),
                metric_value(measures, "coverage") if expected else "SKIPPED",
                metric_value(measures, "bugs") if expected else "SKIPPED",
                metric_value(measures, "vulnerabilities") if expected else "SKIPPED",
                metric_value(measures, "code_smells") if expected else "SKIPPED",
            ]
        )

    report = "\n".join(
        [
            "## SonarQube contextual consolidado",
            "",
            markdown_table(["Contexto", "Quality Gate", *METRICS.values()], rows),
            "",
            "Estados: `PASSED` significa Quality Gate OK; `FAILED` significa Quality Gate remoto reprovado; "
            "`SKIPPED` significa contexto nao selecionado nesta execucao manual; `UNAVAILABLE` significa artifact ou API indisponivel.",
            "",
        ]
    )

    print(report)
    if args.output:
        output = pathlib.Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(report, encoding="utf-8")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
