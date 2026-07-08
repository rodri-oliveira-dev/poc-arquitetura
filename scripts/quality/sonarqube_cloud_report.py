#!/usr/bin/env python3
import argparse
import base64
from dataclasses import dataclass
import json
import os
import pathlib
import sys
import urllib.parse
import urllib.request

from path_security import artifacts_root, repo_root, resolve_output_dir


METRIC_KEYS = [
    "coverage",
    "new_coverage",
    "bugs",
    "new_bugs",
    "vulnerabilities",
    "new_vulnerabilities",
    "code_smells",
    "new_code_smells",
    "security_hotspots",
    "new_security_hotspots",
    "new_security_hotspots_reviewed",
    "duplicated_lines_density",
    "new_duplicated_lines_density",
    "ncloc",
    "reliability_rating",
    "new_reliability_rating",
    "security_rating",
    "new_security_rating",
    "sqale_rating",
    "new_maintainability_rating",
]

ALLOWED_HOST_URL = "https://sonarcloud.io"
ALLOWED_API_ENDPOINTS = frozenset(
    {
        "qualitygates/project_status",
        "measures/component",
        "issues/search",
    }
)

QUALITY_GATE_FILE = "quality-gate.json"
MEASURES_FILE = "measures.json"
ISSUES_FILE = "issues.json"
REPORT_FILE = "sonarqube-cloud-report.md"
REPORT_ALIAS_FILE = "report.md"
REPORT_ARTIFACT_LABEL = "relatorio Markdown SonarQube Cloud"
REPORT_ALIAS_ARTIFACT_LABEL = "alias Markdown SonarQube Cloud"
MARKDOWN_ARTIFACT_FILES = {
    REPORT_ARTIFACT_LABEL: REPORT_FILE,
    REPORT_ALIAS_ARTIFACT_LABEL: REPORT_ALIAS_FILE,
}


@dataclass(frozen=True)
class SonarReportPaths:
    quality_gate: pathlib.Path
    measures: pathlib.Path
    issues: pathlib.Path
    report: pathlib.Path
    report_alias: pathlib.Path

    @property
    def output_dir(self) -> pathlib.Path:
        return self.report.parent


def resolve_report_paths(configured_output_dir: str | pathlib.Path) -> SonarReportPaths:
    output_dir = resolve_output_dir(
        configured_output_dir,
        artifacts_root(),
        base_dir=repo_root(),
        label="diretorio de saida SonarQube Cloud",
    )
    markdown_paths = {
        label: output_dir / file_name
        for label, file_name in MARKDOWN_ARTIFACT_FILES.items()
    }
    return SonarReportPaths(
        quality_gate=output_dir / QUALITY_GATE_FILE,
        measures=output_dir / MEASURES_FILE,
        issues=output_dir / ISSUES_FILE,
        report=markdown_paths[REPORT_ARTIFACT_LABEL],
        report_alias=markdown_paths[REPORT_ALIAS_ARTIFACT_LABEL],
    )


def validate_sonarcloud_host_url(host_url: str) -> str:
    parsed = urllib.parse.urlparse(host_url)
    if parsed.scheme != "https":
        raise ValueError("SONAR_HOST_URL deve usar https.")
    if parsed.hostname != "sonarcloud.io":
        raise ValueError("SONAR_HOST_URL deve apontar para sonarcloud.io.")
    if parsed.username or parsed.password:
        raise ValueError("SONAR_HOST_URL nao deve conter userinfo.")
    if parsed.port is not None:
        raise ValueError("SONAR_HOST_URL nao deve declarar porta.")
    if parsed.path not in ("", "/") or parsed.params or parsed.query or parsed.fragment:
        raise ValueError("SONAR_HOST_URL deve conter apenas a origem https://sonarcloud.io.")
    return ALLOWED_HOST_URL


def validate_api_endpoint(path: str) -> str:
    if path not in ALLOWED_API_ENDPOINTS:
        raise ValueError(f"Endpoint SonarQube Cloud nao permitido: {path}.")
    return path


def write_json_artifacts(paths: SonarReportPaths, quality_gate: dict, measures: dict, issues: dict) -> None:
    for artifact_path, payload in (
        (paths.quality_gate, quality_gate),
        (paths.measures, measures),
        (paths.issues, issues),
    ):
        artifact_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown_artifacts(paths: SonarReportPaths, report: str) -> None:
    paths.report.write_text(report, encoding="utf-8")
    paths.report_alias.write_text(report, encoding="utf-8")


def markdown_table(headers: list[str], rows: list[list[str]]) -> list[str]:
    lines = [
        "| " + " | ".join(headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(str(cell) for cell in row) + " |")
    return lines


def escape_cell(value: object) -> str:
    return str(value if value is not None else "").replace("|", "\\|").replace("\n", " ")


def request_json(host_url: str, sonar_token: str, path: str, query: dict) -> dict:
    base_url = validate_sonarcloud_host_url(host_url)
    endpoint = validate_api_endpoint(path)
    url = base_url + "/api/" + endpoint + "?" + urllib.parse.urlencode(query)
    credentials = base64.b64encode(f"{sonar_token}:".encode("utf-8")).decode("ascii")
    request = urllib.request.Request(
        url,
        headers={
            "Authorization": f"Basic {credentials}",
            "Accept": "application/json",
        },
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def build_header(project_key: str, organization_key: str, dashboard_url: str, pull_request_number: str | None) -> list[str]:
    return [
        "# SonarQube Cloud Report",
        "",
        f"- Projeto: `{project_key}`",
        f"- Organizacao: `{organization_key}`",
        f"- Contexto: `pullRequest={pull_request_number}`" if pull_request_number else "- Contexto: `project`",
        f"- Dashboard: [{project_key}]({dashboard_url})",
        "",
    ]


def build_error_section(error: str) -> list[str]:
    return [
        "## Status",
        "",
        f"Relatorio SonarQube Cloud nao gerado: {error}",
        "",
        "O workflow preservou os artifacts disponiveis e esta etapa nao bloqueou o job.",
        "",
    ]


def build_quality_gate_section(quality_gate: dict | None) -> list[str]:
    project_status = (quality_gate or {}).get("projectStatus", {})
    return [
        "## Quality Gate",
        "",
        f"Status: **{project_status.get('status', 'UNKNOWN')}**",
        "",
    ]


def build_metrics_section(measures: dict | None) -> list[str]:
    measures_by_key = {
        item.get("metric"): item.get("value", next((period.get("value", "") for period in item.get("periods", [])), ""))
        for item in (measures or {}).get("component", {}).get("measures", [])
    }
    lines = ["## Metricas principais", ""]
    lines.extend(markdown_table(["Metrica", "Valor"], [[key, measures_by_key.get(key, "")] for key in METRIC_KEYS]))
    return lines


def build_conditions_section(quality_gate: dict | None) -> list[str]:
    project_status = (quality_gate or {}).get("projectStatus", {})
    condition_rows = [
        [
            escape_cell(condition.get("metricKey", "")),
            escape_cell(condition.get("status", "")),
            escape_cell(condition.get("actualValue", "")),
            escape_cell(condition.get("comparator", "")),
            escape_cell(condition.get("errorThreshold", "")),
        ]
        for condition in project_status.get("conditions", [])
    ]
    lines = ["", "## Condicoes do Quality Gate", ""]
    if condition_rows:
        lines.extend(markdown_table(["Metrica", "Status", "Valor", "Comparador", "Limite"], condition_rows))
    else:
        lines.append("Nenhuma condicao retornada pela API.")
    return lines


def build_issues_summary(issues: dict | None) -> list[str]:
    total_issues = (issues or {}).get("total", 0)
    facets = {facet.get("property"): facet.get("values", []) for facet in (issues or {}).get("facets", [])}
    severity_rows = [[escape_cell(item.get("val", "")), escape_cell(item.get("count", 0))] for item in facets.get("severities", [])]
    type_rows = [[escape_cell(item.get("val", "")), escape_cell(item.get("count", 0))] for item in facets.get("types", [])]

    lines = ["", "## Issues abertas", "", f"Total: **{total_issues}**", "", "### Por severidade", ""]
    lines.extend(markdown_table(["Severidade", "Total"], severity_rows) if severity_rows else ["Nenhuma issue aberta por severidade."])
    lines.extend(["", "### Por tipo", ""])
    lines.extend(markdown_table(["Tipo", "Total"], type_rows) if type_rows else ["Nenhuma issue aberta por tipo."])
    return lines


def build_issue_rows(issues: dict | None) -> list[str]:
    issue_rows = []
    for issue in (issues or {}).get("issues", [])[:20]:
        component = issue.get("component", "")
        file_path = component.split(":", 1)[-1] if ":" in component else component
        issue_rows.append(
            [
                escape_cell(issue.get("severity", "")),
                escape_cell(issue.get("type", "")),
                escape_cell(file_path),
                escape_cell(issue.get("line", "")),
                escape_cell(issue.get("message", "")),
            ]
        )

    lines = ["", "### Top 20 issues abertas", ""]
    if issue_rows:
        lines.extend(markdown_table(["Severidade", "Tipo", "Arquivo", "Linha", "Mensagem"], issue_rows))
    else:
        lines.append("Nenhuma issue aberta retornada pela API.")
    return lines


def build_report(
    project_key: str,
    organization_key: str,
    dashboard_url: str,
    pull_request_number: str | None,
    quality_gate: dict | None = None,
    measures: dict | None = None,
    issues: dict | None = None,
    error: str | None = None,
) -> str:
    lines = build_header(project_key, organization_key, dashboard_url, pull_request_number)

    if error:
        lines.extend(build_error_section(error))
        return "\n".join(lines) + "\n"

    lines.extend(build_quality_gate_section(quality_gate))
    lines.extend(build_metrics_section(measures))
    lines.extend(build_conditions_section(quality_gate))
    lines.extend(build_issues_summary(issues))
    lines.extend(build_issue_rows(issues))
    return "\n".join(lines) + "\n"


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Gera snapshot Markdown/JSON da API do SonarQube Cloud.")
    parser.add_argument("--project-key", default=os.environ.get("SONAR_PROJECT_KEY", ""))
    parser.add_argument("--organization-key", default=os.environ.get("SONAR_ORGANIZATION_KEY", ""))
    parser.add_argument("--output-dir", default=os.environ.get("SONAR_REPORT_DIR", "artifacts/sonarqube"))
    parser.add_argument("--pull-request", default=os.environ.get("SONAR_PULL_REQUEST", ""))
    parser.add_argument("--host-url", default=os.environ.get("SONAR_HOST_URL", "https://sonarcloud.io"))
    parser.add_argument("--token-env", default="SONAR_TOKEN")
    return parser.parse_args(argv)


def main_with_args(argv: list[str] | None = None) -> int:
    args = parse_args(argv)

    if not args.project_key:
        print("project key nao informado.", file=sys.stderr)
        return 2
    if not args.organization_key:
        print("organization key nao informado.", file=sys.stderr)
        return 2

    try:
        report_paths = resolve_report_paths(args.output_dir)
        host_url = validate_sonarcloud_host_url(args.host_url)
    except ValueError as exc:
        print(exc, file=sys.stderr)
        return 2

    report_paths.output_dir.mkdir(parents=True, exist_ok=True)

    sonar_token = os.environ.get(args.token_env, "")
    pull_request_number = args.pull_request or None
    sonar_context_query = {"pullRequest": pull_request_number} if pull_request_number else {}
    dashboard_query = {"id": args.project_key, **sonar_context_query}
    dashboard_url = host_url + "/project/overview?" + urllib.parse.urlencode(dashboard_query)

    try:
        if not sonar_token:
            message = f"{args.token_env} nao esta configurado; pulando consulta a API do SonarQube Cloud."
            write_json_artifacts(report_paths, {"error": message}, {"error": message}, {"error": message})
            report = build_report(args.project_key, args.organization_key, dashboard_url, pull_request_number, error=message)
            write_markdown_artifacts(report_paths, report)
            print(message)
            return 0

        quality_gate = request_json(
            host_url,
            sonar_token,
            "qualitygates/project_status",
            {"projectKey": args.project_key, **sonar_context_query},
        )
        measures = request_json(
            host_url,
            sonar_token,
            "measures/component",
            {
                "component": args.project_key,
                "metricKeys": ",".join(METRIC_KEYS),
                **sonar_context_query,
            },
        )
        issues = request_json(
            host_url,
            sonar_token,
            "issues/search",
            {
                "componentKeys": args.project_key,
                "resolved": "false",
                "facets": "severities,types",
                "ps": "100",
                **sonar_context_query,
            },
        )

        write_json_artifacts(report_paths, quality_gate, measures, issues)

        report = build_report(args.project_key, args.organization_key, dashboard_url, pull_request_number, quality_gate, measures, issues)
        write_markdown_artifacts(report_paths, report)
    except Exception as exc:
        message = f"{type(exc).__name__}: {exc}"
        write_json_artifacts(report_paths, {"error": message}, {"error": message}, {"error": message})
        report = build_report(args.project_key, args.organization_key, dashboard_url, pull_request_number, error=message)
        write_markdown_artifacts(report_paths, report)
        print(f"SonarQube Cloud report could not be generated: {message}")

    return 0


def main() -> int:
    return main_with_args()


if __name__ == "__main__":
    raise SystemExit(main())
