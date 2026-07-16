#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


HTTP_METHODS = {"get", "post", "put", "patch", "delete", "head", "options", "trace"}


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def operation_key(method: str, path: str) -> str:
    return f"{method.upper()} {path}"


def candidate_parameter_examples(config: dict[str, Any], api: str) -> dict[str, Any]:
    examples: dict[str, Any] = {}
    defaults = config.get("parameters")
    if isinstance(defaults, dict):
        examples.update(defaults)

    apis = config.get("apis")
    if isinstance(apis, dict):
        api_config = apis.get(api)
        if isinstance(api_config, dict) and isinstance(api_config.get("parameters"), dict):
            examples.update(api_config["parameters"])

    return examples


def candidate_operation_examples(config: dict[str, Any], api: str) -> dict[str, Any]:
    apis = config.get("apis")
    if not isinstance(apis, dict):
        return {}
    api_config = apis.get(api)
    if not isinstance(api_config, dict):
        return {}
    operations = api_config.get("operations")
    return operations if isinstance(operations, dict) else {}


def infer_parameter_example(parameter: dict[str, Any], configured: dict[str, Any]) -> Any | None:
    name = parameter.get("name")
    if isinstance(name, str) and name in configured:
        return configured[name]

    schema = parameter.get("schema")
    if not isinstance(schema, dict):
        return None

    schema_format = schema.get("format")
    schema_type = schema.get("type")
    if schema_format == "uuid":
        return "11111111-1111-4111-8111-111111111111"
    if schema_format == "date":
        return "2026-01-15"
    if schema_format == "date-time":
        return "2026-01-15T10:00:00Z"
    if schema_type == "integer":
        return 1
    if schema_type == "number":
        return 10.5
    if schema_type == "boolean":
        return True

    return None


def set_parameter_example(parameter: dict[str, Any], configured: dict[str, Any]) -> bool:
    if "example" in parameter or "examples" in parameter:
        return False

    schema = parameter.get("schema")
    if isinstance(schema, dict) and ("example" in schema or "examples" in schema):
        return False

    example = infer_parameter_example(parameter, configured)
    if example is None:
        return False

    parameter["example"] = example
    return True


def set_request_body_example(operation: dict[str, Any], configured: dict[str, Any]) -> bool:
    request_body = operation.get("requestBody")
    if not isinstance(request_body, dict):
        return False

    body_example = configured.get("requestBody")
    if body_example is None:
        return False

    content = request_body.get("content")
    if not isinstance(content, dict):
        return False

    changed = False
    for content_type in ("application/json", "text/json"):
        media_type = content.get(content_type)
        if not isinstance(media_type, dict):
            continue
        if "example" in media_type or "examples" in media_type:
            continue
        media_type["example"] = body_example
        changed = True

    return changed


def enrich(document: dict[str, Any], config: dict[str, Any], api: str) -> tuple[dict[str, Any], int]:
    parameter_examples = candidate_parameter_examples(config, api)
    operation_examples = candidate_operation_examples(config, api)
    changed = 0

    paths = document.get("paths")
    if not isinstance(paths, dict):
        return document, changed

    for path, path_item in paths.items():
        if not isinstance(path, str) or not isinstance(path_item, dict):
            continue

        inherited_parameters = path_item.get("parameters")
        if isinstance(inherited_parameters, list):
            for parameter in inherited_parameters:
                if isinstance(parameter, dict) and set_parameter_example(parameter, parameter_examples):
                    changed += 1

        for method, operation in path_item.items():
            if method.lower() not in HTTP_METHODS or not isinstance(operation, dict):
                continue

            configured_operation = operation_examples.get(operation_key(method, path), {})
            configured_parameters = dict(parameter_examples)
            if isinstance(configured_operation, dict) and isinstance(configured_operation.get("parameters"), dict):
                configured_parameters.update(configured_operation["parameters"])

            parameters = operation.get("parameters")
            if isinstance(parameters, list):
                for parameter in parameters:
                    if isinstance(parameter, dict) and set_parameter_example(parameter, configured_parameters):
                        changed += 1

            if isinstance(configured_operation, dict) and set_request_body_example(operation, configured_operation):
                changed += 1

    return document, changed


def main() -> int:
    parser = argparse.ArgumentParser(description="Enriquece uma copia OpenAPI com exemplos para o OWASP ZAP.")
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--examples", type=Path, required=True)
    parser.add_argument("--api", required=True)
    parser.add_argument("--summary-output", type=Path)
    args = parser.parse_args()

    document = load_json(args.input)
    config = load_json(args.examples)
    if not isinstance(document, dict):
        raise ValueError("Documento OpenAPI precisa ser um objeto JSON.")
    if not isinstance(config, dict):
        raise ValueError("Arquivo de exemplos precisa ser um objeto JSON.")

    enriched, changed = enrich(document, config, args.api)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(enriched, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    if args.summary_output:
        args.summary_output.write_text(f"OPENAPI_EXAMPLES_APPLIED={changed}\n", encoding="utf-8")

    print(f"OpenAPI enriquecido para {args.api}: {changed} exemplo(s) aplicado(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
