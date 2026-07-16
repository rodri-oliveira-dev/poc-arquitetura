#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
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
AUXILIARY_JSON_SUFFIXES = (
    "-openapi-raw.json",
    "-openapi-zap.json",
)


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


@dataclass(frozen=True)
class OperationManifest:
    total: int
    business: int


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


def load_openapi_operation_counts(root: Path) -> dict[str, OperationManifest]:
    counts: dict[str, OperationManifest] = {}
    for manifest in root.rglob("*-openapi-operations.txt"):
        total_operations = 0
        business_operations = 0
        for line in manifest.read_text(encoding="utf-8", errors="replace").splitlines():
            match = re.match(r"^(GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS|TRACE)\s+(.+)$", line)
            if match:
                total_operations += 1
                if is_business_uri(match.group(2)):
                    business_operations += 1
        counts[manifest.name.removesuffix("-openapi-operations.txt")] = OperationManifest(
            total=total_operations,
            business=business_operations,
        )
    return counts


def status_bucket(status: int) -> str:
    return f"{status // 100}xx" if 100 <= status <= 599 else "other"


def observed_operation_count(requests: list[ObservedRequest]) -> int:
    return len({(request.method, urlsplit(request.uri).path) for request in requests})


def load_accepted_alerts(path: Path | None) -> list[dict[str, Any]]:
    if path is None:
        return []
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("Arquivo de allowlist de alertas precisa ser um objeto JSON.")
    alerts = payload.get("accepted_alerts", [])
    if not isinstance(alerts, list):
        raise ValueError("Campo accepted_alerts precisa ser uma lista.")
    return [item for item in alerts if isinstance(item, dict)]


def is_auxiliary_json(report: Path, accepted_alerts_file: Path | None) -> bool:
    if any(report.name.endswith(suffix) for suffix in AUXILIARY_JSON_SUFFIXES):
        return True
    if accepted_alerts_file is not None:
        try:
            return report.resolve() == accepted_alerts_file.resolve()
        except OSError:
            return False
    return False


def alert_is_accepted(alert: Alert, accepted_alerts: list[dict[str, Any]]) -> bool:
    today = dt.date.today()
    for accepted in accepted_alerts:
        name = accepted.get("name")
        severity = accepted.get("severity")
        apis = accepted.get("apis", ["*"])
        expires = accepted.get("expires")

        if isinstance(expires, str):
            try:
                if dt.date.fromisoformat(expires) < today:
                    continue
            except ValueError:
                continue

        if isinstance(name, str) and name != alert.name:
            continue
        if isinstance(severity, str) and severity != alert.severity:
            continue
        if isinstance(apis, list) and "*" not in apis and alert.api not in apis:
            continue
        return True

    return False


def build_summary(
    reports: list[Path],
    requests: list[ObservedRequest],
    alerts: list[Alert],
    operation_counts: dict[str, OperationManifest],
    accepted_alert_count: int,
    failures: list[str],
    warnings: list[str],
) -> str:
    lines = ["# OWASP ZAP authenticated coverage", ""]
    lines.append(f"- Relatorios JSON analisados: `{len(reports)}`")
    lines.append(f"- Operacoes de negocio observadas: `{len(requests)}`")
    lines.append(f"- Alertas aceitos por allowlist: `{accepted_alert_count}`")
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
        manifest = operation_counts.get(api, operation_counts.get(api.replace("-api", "-service-api"), OperationManifest(0, 0)))
        observed_operations = observed_operation_count(api_requests)
        coverage = 0.0 if manifest.business == 0 else observed_operations * 100 / manifest.business

        lines.append(f"### {api}")
        lines.append(f"- Operacoes OpenAPI declaradas: `{manifest.total}`")
        lines.append(f"- Operacoes OpenAPI de negocio declaradas: `{manifest.business}`")
        lines.append(f"- Operacoes de negocio observadas: `{len(api_requests)}`")
        lines.append(f"- Operacoes de negocio distintas observadas: `{observed_operations}`")
        lines.append(f"- Cobertura aproximada de negocio: `{coverage:.1f}%`")
        lines.append(f"- Status HTTP: `{dict(sorted(exact_status_counts.items()))}`")
        lines.append(f"- 2xx: `{status_counts.get('2xx', 0)}`")
        lines.append(f"- 4xx: `{status_counts.get('4xx', 0)}`")
        lines.append(f"- 5xx: `{status_counts.get('5xx', 0)}`")
        lines.append(f"- Alertas por severidade: `{dict(sorted(alert_counts.items()))}`")
        lines.append("")

    if warnings:
        lines.append("## Avisos")
        lines.append("")
        for warning in warnings:
            lines.append(f"- {warning}")
        lines.append("")

    if failures:
        lines.append("## Falhas")
        lines.append("")
        for failure in failures:
            lines.append(f"- {failure}")
        lines.append("")

    return "\n".join(lines)


def validate(
    root: Path,
    summary_output: Path | None,
    fail_on_alerts: bool,
    accepted_alerts_file: Path | None,
    min_business_operations_per_api: int,
    min_business_coverage_percent: float,
) -> int:
    reports = sorted(report for report in root.rglob("*.json") if not is_auxiliary_json(report, accepted_alerts_file))
    if not reports:
        summary = build_summary([], [], [], {}, 0, ["nenhum relatorio JSON encontrado."], [])
        if summary_output:
            summary_output.parent.mkdir(parents=True, exist_ok=True)
            summary_output.write_text(summary + "\n", encoding="utf-8")
        print(summary)
        return 1

    requests: list[ObservedRequest] = []
    alerts: list[Alert] = []
    failures: list[str] = []
    warnings: list[str] = []
    accepted_alerts = load_accepted_alerts(accepted_alerts_file)
    accepted_alert_count = 0

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

    if fail_on_alerts:
        for alert in alerts:
            if alert.severity in {"High", "Medium"}:
                if alert_is_accepted(alert, accepted_alerts):
                    accepted_alert_count += 1
                    continue
                failures.append(f"{alert.report}: alerta {alert.severity} - {alert.name}.")

    if not requests:
        failures.append("nenhuma operacao de negocio em /api/ foi observada.")

    operation_counts = load_openapi_operation_counts(root)

    requests_by_api: dict[str, list[ObservedRequest]] = defaultdict(list)
    for request in requests:
        requests_by_api[request.api].append(request)

    for api, manifest in sorted(operation_counts.items()):
        if manifest.business <= 0:
            continue
        api_requests = requests_by_api.get(api, [])
        distinct_observed = observed_operation_count(api_requests)
        coverage = distinct_observed * 100 / manifest.business

        if min_business_operations_per_api > 0 and distinct_observed < min_business_operations_per_api:
            failures.append(
                f"{api}: cobertura insuficiente; {distinct_observed} operacao(oes) de negocio distinta(s) "
                f"observada(s), minimo configurado {min_business_operations_per_api}."
            )
        if min_business_coverage_percent > 0 and coverage < min_business_coverage_percent:
            failures.append(
                f"{api}: cobertura aproximada de negocio {coverage:.1f}% abaixo do minimo "
                f"{min_business_coverage_percent:.1f}%."
            )
        if api_requests and not any(200 <= request.status <= 399 for request in api_requests):
            warnings.append(f"{api}: nenhuma operacao de negocio retornou 2xx/3xx; revise massa de dados/exemplos do scan.")

    summary = build_summary(reports, requests, alerts, operation_counts, accepted_alert_count, failures, warnings)

    if summary_output:
        summary_output.parent.mkdir(parents=True, exist_ok=True)
        summary_output.write_text(summary + "\n", encoding="utf-8")

    print(summary)
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Valida cobertura autenticada dos relatorios JSON do OWASP ZAP.")
    parser.add_argument("--reports-root", type=Path, required=True)
    parser.add_argument("--summary-output", type=Path)
    parser.add_argument(
        "--fail-on-alerts",
        action="store_true",
        help="Falha em alertas ZAP High/Medium alem do gate obrigatorio de autenticacao.",
    )
    parser.add_argument(
        "--accepted-alerts",
        type=Path,
        help="Allowlist JSON versionada para alertas High/Medium conhecidos quando --fail-on-alerts estiver ativo.",
    )
    parser.add_argument(
        "--min-business-operations-per-api",
        type=int,
        default=0,
        help="Minimo de operacoes /api/ distintas observadas por API com operacoes de negocio declaradas.",
    )
    parser.add_argument(
        "--min-business-coverage-percent",
        type=float,
        default=0.0,
        help="Percentual minimo aproximado de operacoes /api/ distintas observadas por API.",
    )
    args = parser.parse_args()

    return validate(
        args.reports_root,
        args.summary_output,
        args.fail_on_alerts,
        args.accepted_alerts,
        args.min_business_operations_per_api,
        args.min_business_coverage_percent,
    )


if __name__ == "__main__":
    sys.exit(main())
