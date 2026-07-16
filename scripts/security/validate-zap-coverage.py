#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import parse_qsl, urlencode, urlsplit, urlunsplit


BUSINESS_PATH_MARKER = "/api/"
IGNORED_PATH_MARKERS = (
    "/health",
    "/ready",
    "/swagger",
    "/openapi",
    "/metrics",
)
HTTP_METHODS = {"GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE"}
SENSITIVE_QUERY_KEYS = {
    "access_token",
    "authorization",
    "client_secret",
    "password",
    "refresh_token",
    "secret",
    "token",
}


@dataclass(frozen=True)
class ObservedRequest:
    report: Path
    api: str
    method: str
    uri: str
    status: int


@dataclass(frozen=True)
class Alert:
    report: Path
    api: str
    name: str
    severity: str


def infer_api(report: Path, payload: Any) -> str:
    if report.stem:
        return report.stem

    if isinstance(payload, dict):
        site = payload.get("site")
        if isinstance(site, list) and site:
            first = site[0]
            if isinstance(first, dict):
                name = first.get("@name") or first.get("name")
                if isinstance(name, str) and name:
                    return name
        name = payload.get("@name") or payload.get("name")
        if isinstance(name, str) and name:
            return name

    return report.stem


def redact_uri(uri: str) -> str:
    split = urlsplit(uri)
    if not split.query:
        return uri

    query = []
    for key, value in parse_qsl(split.query, keep_blank_values=True):
        if key.lower() in SENSITIVE_QUERY_KEYS:
            query.append((key, "<redacted>"))
        else:
            query.append((key, value))

    return urlunsplit((split.scheme, split.netloc, split.path, urlencode(query), split.fragment))


def normalize_method(value: Any) -> str | None:
    if not isinstance(value, str):
        return None
    method = value.upper()
    return method if method in HTTP_METHODS else None


def normalize_status(value: Any) -> int | None:
    if isinstance(value, int):
        return value
    if isinstance(value, str) and re.fullmatch(r"\d{3}", value.strip()):
        return int(value.strip())
    return None


def extract_uri(node: dict[str, Any]) -> str | None:
    for key in ("uri", "url", "href", "requestUri", "request_uri"):
        value = node.get(key)
        if isinstance(value, str) and value:
            return value

    request_header = node.get("requestHeader") or node.get("request_header")
    if isinstance(request_header, str):
        first_line = request_header.splitlines()[0] if request_header.splitlines() else ""
        parts = first_line.split()
        if len(parts) >= 2 and parts[0].upper() in HTTP_METHODS:
            return parts[1]

    return None


def extract_method(node: dict[str, Any]) -> str | None:
    for key in ("method", "requestMethod", "request_method"):
        method = normalize_method(node.get(key))
        if method:
            return method

    request_header = node.get("requestHeader") or node.get("request_header")
    if isinstance(request_header, str):
        first_line = request_header.splitlines()[0] if request_header.splitlines() else ""
        parts = first_line.split()
        if parts:
            return normalize_method(parts[0])

    return None


def extract_status(node: dict[str, Any]) -> int | None:
    for key in ("status", "statusCode", "status_code", "responseStatus", "response_status"):
        status = normalize_status(node.get(key))
        if status is not None:
            return status

    evidence_status = normalize_status(node.get("evidence"))
    if evidence_status is not None:
        return evidence_status

    response_header = node.get("responseHeader") or node.get("response_header")
    if isinstance(response_header, str):
        first_line = response_header.splitlines()[0] if response_header.splitlines() else ""
        match = re.search(r"\b(\d{3})\b", first_line)
        if match:
            return int(match.group(1))

    return None


def walk_dicts(node: Any) -> list[dict[str, Any]]:
    found: list[dict[str, Any]] = []
    if isinstance(node, dict):
        found.append(node)
        for value in node.values():
            found.extend(walk_dicts(value))
    elif isinstance(node, list):
        for item in node:
            found.extend(walk_dicts(item))
    return found


def is_business_uri(uri: str) -> bool:
    lowered = uri.lower()
    if BUSINESS_PATH_MARKER not in lowered:
        return False
    return not any(marker in lowered for marker in IGNORED_PATH_MARKERS)


def extract_requests(report: Path, api: str, payload: Any) -> list[ObservedRequest]:
    requests: list[ObservedRequest] = []
    seen: set[tuple[str, str, int]] = set()

    for node in walk_dicts(payload):
        method = extract_method(node)
        uri = extract_uri(node)
        status = extract_status(node)
        if method is None or uri is None or status is None:
            continue
        if not is_business_uri(uri):
            continue

        sanitized_uri = redact_uri(uri)
        key = (method, sanitized_uri, status)
        if key in seen:
            continue
        seen.add(key)
        requests.append(ObservedRequest(report, api, method, sanitized_uri, status))

    return requests


def normalize_severity(value: Any) -> str:
    text = str(value or "").strip().lower()
    if "high" in text or text == "3":
        return "High"
    if "medium" in text or text == "2":
        return "Medium"
    if "low" in text or text == "1":
        return "Low"
    if "informational" in text or "info" in text or text == "0":
        return "Informational"
    return "Unknown"


def extract_alerts(report: Path, api: str, payload: Any) -> list[Alert]:
    alerts: list[Alert] = []
    seen: set[tuple[str, str]] = set()

    for node in walk_dicts(payload):
        if not any(key in node for key in ("riskdesc", "risk", "riskcode", "severity")):
            continue

        severity = normalize_severity(
            node.get("riskdesc")
            or node.get("risk")
            or node.get("riskcode")
            or node.get("severity")
        )
        name = str(node.get("alert") or node.get("name") or node.get("pluginid") or "alerta ZAP")
        key = (name, severity)
        if key in seen:
            continue
        seen.add(key)
        alerts.append(Alert(report, api, name, severity))

    return alerts


def load_openapi_operation_counts(root: Path) -> dict[str, int]:
    counts: dict[str, int] = {}
    for manifest in root.rglob("*-openapi-operations.txt"):
        operations = 0
        for line in manifest.read_text(encoding="utf-8", errors="replace").splitlines():
            if re.match(r"^(GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS|TRACE)\s+", line):
                operations += 1
        counts[manifest.name.removesuffix("-openapi-operations.txt")] = operations
    return counts


def status_bucket(status: int) -> str:
    return f"{status // 100}xx" if 100 <= status <= 599 else "other"


def build_summary(
    reports: list[Path],
    requests: list[ObservedRequest],
    alerts: list[Alert],
    operation_counts: dict[str, int],
    failures: list[str],
) -> str:
    lines = ["# OWASP ZAP authenticated coverage", ""]
    lines.append(f"- Relatorios JSON analisados: `{len(reports)}`")
    lines.append(f"- Operacoes de negocio observadas: `{len(requests)}`")
    lines.append(f"- Resultado do gate: `{'falha' if failures else 'sucesso'}`")
    lines.append("")
    lines.append("## APIs")
    lines.append("")

    by_api: dict[str, list[ObservedRequest]] = defaultdict(list)
    alerts_by_api: dict[str, list[Alert]] = defaultdict(list)
    for request in requests:
        by_api[request.api].append(request)
    for alert in alerts:
        alerts_by_api[alert.api].append(alert)

    api_names = sorted(set(by_api) | set(alerts_by_api) | set(operation_counts))
    for api in api_names:
        api_requests = by_api.get(api, [])
        status_counts = Counter(status_bucket(item.status) for item in api_requests)
        exact_status_counts = Counter(str(item.status) for item in api_requests)
        alert_counts = Counter(alert.severity for alert in alerts_by_api.get(api, []))
        declared = operation_counts.get(api, operation_counts.get(api.replace("-api", "-service-api"), 0))

        lines.append(f"### {api}")
        lines.append(f"- Operacoes OpenAPI declaradas: `{declared}`")
        lines.append(f"- Operacoes de negocio observadas: `{len(api_requests)}`")
        lines.append(f"- Status HTTP: `{dict(sorted(exact_status_counts.items()))}`")
        lines.append(f"- 2xx: `{status_counts.get('2xx', 0)}`")
        lines.append(f"- 4xx: `{status_counts.get('4xx', 0)}`")
        lines.append(f"- 5xx: `{status_counts.get('5xx', 0)}`")
        lines.append(f"- Alertas por severidade: `{dict(sorted(alert_counts.items()))}`")
        lines.append("")

    if failures:
        lines.append("## Falhas")
        lines.append("")
        for failure in failures:
            lines.append(f"- {failure}")
        lines.append("")

    return "\n".join(lines)


def validate(root: Path, summary_output: Path | None) -> int:
    reports = sorted(root.rglob("*.json"))
    if not reports:
        summary = build_summary([], [], [], {}, ["nenhum relatorio JSON encontrado."])
        if summary_output:
            summary_output.parent.mkdir(parents=True, exist_ok=True)
            summary_output.write_text(summary + "\n", encoding="utf-8")
        print(summary)
        return 1

    requests: list[ObservedRequest] = []
    alerts: list[Alert] = []
    failures: list[str] = []

    for report in reports:
        try:
            payload = json.loads(report.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            failures.append(f"{report}: JSON invalido ({exc.msg}).")
            continue

        api = infer_api(report, payload)
        requests.extend(extract_requests(report, api, payload))
        alerts.extend(extract_alerts(report, api, payload))

    for request in requests:
        if request.status in (401, 403):
            failures.append(
                f"{request.report}: {request.method} {request.uri} -> HTTP {request.status}."
            )

    for alert in alerts:
        if alert.severity in {"High", "Medium"}:
            failures.append(f"{alert.report}: alerta {alert.severity} - {alert.name}.")

    if not requests:
        failures.append("nenhuma operacao de negocio em /api/ foi observada.")

    operation_counts = load_openapi_operation_counts(root)
    summary = build_summary(reports, requests, alerts, operation_counts, failures)

    if summary_output:
        summary_output.parent.mkdir(parents=True, exist_ok=True)
        summary_output.write_text(summary + "\n", encoding="utf-8")

    print(summary)
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Valida cobertura autenticada dos relatorios JSON do OWASP ZAP.")
    parser.add_argument("--reports-root", type=Path, required=True)
    parser.add_argument("--summary-output", type=Path)
    args = parser.parse_args()

    return validate(args.reports_root, args.summary_output)


if __name__ == "__main__":
    sys.exit(main())
