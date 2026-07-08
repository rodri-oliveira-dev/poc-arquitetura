#!/usr/bin/env python3
import argparse
import json
import pathlib
import sys

from path_security import quality_scripts_root, repo_root, resolve_existing_file


ENV_KEYS = {
    "context": "SONAR_CONTEXT",
    "solution": "SOLUTION_PATH",
    "projectKey": "SONAR_PROJECT_KEY",
    "projectName": "SONAR_PROJECT_NAME",
    "resultsDir": "TEST_RESULTS_DIR",
    "sonarReportDir": "SONAR_REPORT_DIR",
    "coverageReportPattern": "SONAR_OPENCOVER_REPORTS_PATHS",
}


def load_contexts(path: pathlib.Path) -> dict:
    config_path = resolve_existing_file(
        path,
        quality_scripts_root(),
        base_dir=repo_root(),
        label="arquivo de configuracao Sonar",
    )
    with config_path.open(encoding="utf-8") as file:
        contexts = json.load(file)

    required = set(ENV_KEYS) - {"context"}
    for name, config in contexts.items():
        missing = sorted(required - set(config))
        if missing:
            raise ValueError(f"Contexto {name!r} sem campos obrigatorios: {', '.join(missing)}")

    return contexts


def normalize_path(value: str) -> str:
    return "./" + value.lstrip("./")


def resolve_context(context_name: str, config_path: pathlib.Path) -> dict:
    contexts = load_contexts(config_path)
    if context_name not in contexts:
        available = ", ".join(sorted(contexts))
        raise KeyError(f"Contexto Sonar desconhecido: {context_name}. Disponiveis: {available}")

    config = dict(contexts[context_name])
    config["context"] = context_name
    config["solution"] = normalize_path(config["solution"])
    config["resultsDir"] = normalize_path(config["resultsDir"])
    config["sonarReportDir"] = normalize_path(config["sonarReportDir"])
    config["coverageReportPattern"] = normalize_path(config["coverageReportPattern"])
    return config


def shell_quote(value: str) -> str:
    return "'" + value.replace("'", "'\"'\"'") + "'"


def print_env(config: dict, github_env: bool) -> None:
    for key, env_name in ENV_KEYS.items():
        value = str(config[key])
        if github_env:
            print(f"{env_name}={value}")
        else:
            print(f"export {env_name}={shell_quote(value)}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Resolve configuracao SonarQube por contexto.")
    parser.add_argument("context", nargs="?", default="global")
    parser.add_argument(
        "--config",
        default=repo_root() / "scripts" / "quality" / "sonar-contexts.json",
        type=pathlib.Path,
    )
    parser.add_argument(
        "--format",
        choices=("json", "shell", "github-env"),
        default="json",
    )
    args = parser.parse_args()

    try:
        config = resolve_context(args.context, args.config)
    except Exception as exc:
        print(exc, file=sys.stderr)
        return 2

    if args.format == "json":
        print(json.dumps(config, indent=2, sort_keys=True))
    else:
        print_env(config, github_env=args.format == "github-env")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
