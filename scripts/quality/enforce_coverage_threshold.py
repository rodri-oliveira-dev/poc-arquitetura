#!/usr/bin/env python3
import argparse
import json
import sys
from pathlib import Path


def parse_required_assemblies(raw: str) -> list[str]:
    return [item.strip() for item in raw.split(",") if item.strip()]


def load_summary(path: Path) -> dict:
    if not path.exists():
        raise FileNotFoundError(f"Coverage summary not found: {path}")

    with path.open(encoding="utf-8") as summary:
        return json.load(summary)


def enforce(summary_file: Path, threshold: float, required_assemblies: list[str]) -> int:
    data = load_summary(summary_file)
    line_coverage = float(data["summary"]["linecoverage"])

    print(f"Line coverage: {line_coverage}%")
    if line_coverage < threshold:
        print(f"Coverage below threshold ({threshold}%).", file=sys.stderr)
        return 1

    assemblies = {
        assembly["name"]: float(assembly["coverage"])
        for assembly in data.get("coverage", {}).get("assemblies", [])
    }

    for assembly_name in required_assemblies:
        if assembly_name not in assemblies:
            print(f"Coverage assembly not found: {assembly_name}", file=sys.stderr)
            return 1

        assembly_line_coverage = assemblies[assembly_name]
        print(f"{assembly_name} line coverage: {assembly_line_coverage}%")
        if assembly_line_coverage < threshold:
            print(
                f"{assembly_name} coverage below threshold ({threshold}%).",
                file=sys.stderr,
            )
            return 1

    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Valida o threshold de cobertura de linhas gerado pelo ReportGenerator."
    )
    parser.add_argument("summary_file", type=Path)
    parser.add_argument("threshold", type=float)
    parser.add_argument("required_assemblies", nargs="?", default="")
    args = parser.parse_args()

    try:
        return enforce(
            args.summary_file,
            args.threshold,
            parse_required_assemblies(args.required_assemblies),
        )
    except (KeyError, TypeError, ValueError, json.JSONDecodeError, FileNotFoundError) as exc:
        print(f"Invalid coverage summary: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
