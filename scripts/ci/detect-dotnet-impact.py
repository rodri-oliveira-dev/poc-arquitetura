#!/usr/bin/env python3
import argparse
import fnmatch
import json
import pathlib
import sys


SERVICES = ("audit", "balance", "identity", "ledger", "payment", "transfer")

DOC_PATTERNS = (
    "docs/**",
    "*.md",
    "*.png",
    "*.jpg",
    "*.jpeg",
    "*.gif",
    "*.svg",
    "*.webp",
)

SHARED_PATTERNS = (
    "PocArquitetura.Shared.slnx",
    "src/Shared/**",
    "tests/Shared/**",
)

AGGREGATE_PATTERNS = (
    "PocArquitetura.slnx",
    "tests/Architecture.Tests/**",
    *(f"{service.capitalize()}Service.slnx" for service in SERVICES),
    *(f"src/{service}/**" for service in SERVICES),
    *(f"tests/{service}/**" for service in SERVICES),
)

GLOBAL_PATTERNS = (
    "*.sln",
    "*.slnx",
    "global.json",
    "NuGet.config",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    ".config/dotnet-tools.json",
    "dotnet-tools.json",
    "coverlet.runsettings",
    "test.sh",
    "test.ps1",
    ".github/actions/**",
    ".github/workflows/**",
    "scripts/ci/**",
    "scripts/quality/**",
    "scripts/contracts/openapi/**",
)

TOOL_PATTERNS = ("tools/**",)


def normalize_path(path: str) -> str:
    normalized = path.strip().lstrip("\ufeff").lstrip("\xef\xbb\xbf").replace("\\", "/")
    while normalized.startswith("./"):
        normalized = normalized[2:]
    return normalized


def matches(path: str, patterns: tuple[str, ...]) -> bool:
    normalized = path.lower()
    return any(fnmatch.fnmatchcase(normalized, pattern.lower()) for pattern in patterns)


def is_csharp(path: str) -> bool:
    return path.lower().endswith(".cs")


def unique_preserving_order(paths: list[str]) -> list[str]:
    seen = set()
    result = []
    for path in paths:
        if path and path not in seen:
            seen.add(path)
            result.append(path)
    return result


def paths_from_json(raw: str) -> list[str]:
    payload = json.loads(raw)
    if not isinstance(payload, list):
        raise ValueError("JSON input must be a list of changed file objects or paths.")

    paths = []
    for item in payload:
        if isinstance(item, str):
            paths.append(item)
            continue

        if not isinstance(item, dict):
            raise ValueError("JSON entries must be strings or objects.")

        filename = item.get("filename")
        previous_filename = item.get("previous_filename")
        if not isinstance(filename, str) or not filename.strip():
            raise ValueError("JSON file objects must contain a non-empty filename.")

        paths.append(filename)
        if isinstance(previous_filename, str) and previous_filename.strip():
            paths.append(previous_filename)

    return paths


def current_paths_from_json(raw: str) -> list[str]:
    payload = json.loads(raw)
    if not isinstance(payload, list):
        raise ValueError("JSON input must be a list of changed file objects or paths.")

    paths = []
    for item in payload:
        if isinstance(item, str):
            paths.append(item)
            continue

        if not isinstance(item, dict):
            raise ValueError("JSON entries must be strings or objects.")

        filename = item.get("filename")
        if not isinstance(filename, str) or not filename.strip():
            raise ValueError("JSON file objects must contain a non-empty filename.")

        paths.append(filename)

    return paths


def paths_from_lines(raw: str) -> list[str]:
    return raw.splitlines()


def clean_raw_input(raw: str) -> str:
    raw = raw.lstrip("\ufeff").lstrip("\xef\xbb\xbf")
    return raw


def parse_changed_paths(raw: str, input_format: str) -> list[str]:
    raw = clean_raw_input(raw)
    if input_format == "json":
        paths = paths_from_json(raw)
    elif input_format == "lines":
        paths = paths_from_lines(raw)
    else:
        stripped = raw.lstrip()
        paths = paths_from_json(raw) if stripped.startswith("[") else paths_from_lines(raw)

    return unique_preserving_order([normalize_path(path) for path in paths if normalize_path(path)])


def parse_current_changed_paths(raw: str, input_format: str) -> list[str]:
    raw = clean_raw_input(raw)
    if input_format == "json":
        paths = current_paths_from_json(raw)
    elif input_format == "lines":
        paths = paths_from_lines(raw)
    else:
        stripped = raw.lstrip()
        paths = current_paths_from_json(raw) if stripped.startswith("[") else paths_from_lines(raw)

    return unique_preserving_order([normalize_path(path) for path in paths if normalize_path(path)])


def fallback_result(reason: str) -> dict:
    return {
        "run_aggregate": True,
        "run_shared": True,
        "docs_only": False,
        "changed_csharp_files": [],
        "changed_csharp_count": 0,
        "classification": [{"path": "<fallback>", "impact": "global", "reason": reason}],
        "detection_failed": True,
    }


def classify_path(path: str) -> tuple[bool, bool, bool, str]:
    if matches(path, DOC_PATTERNS):
        return False, False, True, "documentation"

    if matches(path, SHARED_PATTERNS):
        return True, True, False, "shared"

    if matches(path, AGGREGATE_PATTERNS):
        return True, False, False, "service-or-architecture"

    if matches(path, GLOBAL_PATTERNS):
        return True, True, False, "global"

    if matches(path, TOOL_PATTERNS):
        return True, False, False, "tooling"

    return True, True, False, "unclassified"


def detect_impact(paths: list[str]) -> dict:
    if not paths:
        return fallback_result("No changed files were provided. Running both validations.")

    run_aggregate = False
    run_shared = False
    docs_only = True
    classification = []

    for path in paths:
        aggregate, shared, documentation, reason = classify_path(path)
        if not documentation:
            docs_only = False

        run_aggregate = run_aggregate or aggregate
        run_shared = run_shared or shared
        classification.append(
            {
                "path": path,
                "impact": impact_name(aggregate, shared, documentation),
                "reason": reason,
            }
        )

    if docs_only:
        run_aggregate = False
        run_shared = False

    changed_csharp_files = [path for path in paths if is_csharp(path)]
    return {
        "run_aggregate": run_aggregate,
        "run_shared": run_shared,
        "docs_only": docs_only,
        "changed_csharp_files": changed_csharp_files,
        "changed_csharp_count": len(changed_csharp_files),
        "classification": classification,
        "detection_failed": False,
    }


def impact_name(aggregate: bool, shared: bool, documentation: bool) -> str:
    if documentation:
        return "documentation"
    if aggregate and shared:
        return "global"
    if aggregate:
        return "aggregate"
    if shared:
        return "shared"
    return "none"


def read_input(files_from: pathlib.Path | None) -> str:
    if files_from is None:
        return sys.stdin.read()
    return files_from.read_text(encoding="utf-8")


def bool_output(value: bool) -> str:
    return str(value).lower()


def write_multiline_output(name: str, value: str) -> None:
    delimiter = f"EOF_{name}"
    print(f"{name}<<{delimiter}")
    print(value)
    print(delimiter)


def print_github_output(result: dict) -> None:
    print(f"run_aggregate={bool_output(result['run_aggregate'])}")
    print(f"run_shared={bool_output(result['run_shared'])}")
    print(f"docs_only={bool_output(result['docs_only'])}")
    print(f"detection_failed={bool_output(result['detection_failed'])}")
    print(f"changed_csharp_count={result['changed_csharp_count']}")
    write_multiline_output("changed_csharp_files", "\n".join(result["changed_csharp_files"]))
    write_multiline_output("summary_json", json.dumps(result, separators=(",", ":")))


def write_optional_files(result: dict, changed_csharp_files_output: pathlib.Path | None) -> None:
    if changed_csharp_files_output is not None:
        changed_csharp_files_output.write_text("\n".join(result["changed_csharp_files"]) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Detecta impacto .NET de arquivos alterados no CI.")
    parser.add_argument("--files-from", type=pathlib.Path)
    parser.add_argument("--changed-csharp-files-output", type=pathlib.Path)
    parser.add_argument("--input-format", choices=("auto", "json", "lines"), default="auto")
    parser.add_argument("--format", choices=("json", "github-output"), default="json")
    parser.add_argument("--fallback-reason")
    parser.add_argument(
        "--non-pr-behavior",
        choices=("conservative", "docs-only"),
        default="conservative",
        help="Comportamento quando o workflow nao possui lista de arquivos de PR.",
    )
    args = parser.parse_args()

    if args.fallback_reason:
        result = fallback_result(args.fallback_reason)
    elif args.non_pr_behavior == "docs-only":
        result = detect_impact(["docs/non-pr-placeholder.md"])
    else:
        try:
            raw = read_input(args.files_from)
            result = detect_impact(parse_changed_paths(raw, args.input_format))
            current_csharp_files = [path for path in parse_current_changed_paths(raw, args.input_format) if is_csharp(path)]
            result["changed_csharp_files"] = current_csharp_files
            result["changed_csharp_count"] = len(current_csharp_files)
        except Exception as exc:
            result = fallback_result(f"Could not parse changed files: {exc}. Running both validations.")

    write_optional_files(result, args.changed_csharp_files_output)

    if args.format == "github-output":
        print_github_output(result)
    else:
        print(json.dumps(result, indent=2, sort_keys=True))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
