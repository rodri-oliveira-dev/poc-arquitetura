#!/usr/bin/env python3
import argparse
import base64
import json
import os
import pathlib
import sys
import urllib.parse
import urllib.request


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


def get_pull_request_number(github_event_name: str, github_event_path: str) -> str | None:
    if github_event_name != "pull_request" or not github_event_path:
        return None

    try:
        with open(github_event_path, encoding="utf-8") as event_file:
            event = json.load(event_file)
        number = event.get("pull_request", {}).get("number") or event.get("number")
        return str(number) if number else None
    except Exception:
        return None


def write_json(path: pathlib.Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


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
    url = host_url.rstrip("/") + "/api/" + path + "?" + urllib.parse.urlencode(query)
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
    lines = [
        "# SonarQube Cloud Report",
        "",
        f"- Projeto: `{project_key}`",
        f"- Organizacao: `{organization_key}`",
        f"- Contexto: `pullRequest={pull_request_number}`" if pull_request_number else "- Contexto: `project`",
        f"- Dashboard: [{project_key}]({dashboard_url})",
        "",
    ]

    if error:
        lines.extend(
            [
                "## Status",
                "",
                f"Relatorio SonarQube Cloud nao gerado: {error}",
                "",
                "O workflow preservou os artifacts disponiveis e esta etapa nao bloqueou o job.",
                "",
            ]
        )
        return "\n".join(lines) + "\n"

    project_status = (quality_gate or {}).get("projectStatus", {})
    lines.extend(
        [
            "## Quality Gate",
            "",
            f"Status: **{project_status.get('status', 'UNKNOWN')}**",
            "",
            "## Metricas principais",
            "",
        ]
    )

    measures_by_key = {
        item.get("metric"): item.get("value", next((period.get("value", "") for period in item.get("periods", [])), ""))
        for item in (measures or {}).get("component", {}).get("measures", [])
    }
    lines.extend(markdown_table(["Metrica", "Valor"], [[key, measures_by_key.get(key, "")] for key in METRIC_KEYS]))
    lines.extend(["", "## Condicoes do Quality Gate", ""])

    condition_rows = []
    for condition in project_status.get("conditions", []):
        condition_rows.append(
            [
                escape_cell(condition.get("metricKey", "")),
                escape_cell(condition.get("status", "")),
                escape_cell(condition.get("actualValue", "")),
                escape_cell(condition.get("comparator", "")),
                escape_cell(condition.get("errorThreshold", "")),
            ]
        )
    if condition_rows:
        lines.extend(markdown_table(["Metrica", "Status", "Valor", "Comparador", "Limite"], condition_rows))
    else:
        lines.append("Nenhuma condicao retornada pela API.")

    total_issues = (issues or {}).get("total", 0)
    lines.extend(["", "## Issues abertas", "", f"Total: **{total_issues}**", ""])

    facets = {
        facet.get("property"): facet.get("values", [])
        for facet in (issues or {}).get("facets", [])
    }

    severity_rows = [
        [escape_cell(item.get("val", "")), escape_cell(item.get("count", 0))]
        for item in facets.get("severities", [])
    ]
    lines.extend(["### Por severidade", ""])
    lines.extend(markdown_table(["Severidade", "Total"], severity_rows) if severity_rows else ["Nenhuma issue aberta por severidade."])

    type_rows = [
        [escape_cell(item.get("val", "")), escape_cell(item.get("count", 0))]
        for item in facets.get("types", [])
    ]
    lines.extend(["", "### Por tipo", ""])
    lines.extend(markdown_table(["Tipo", "Total"], type_rows) if type_rows else ["Nenhuma issue aberta por tipo."])

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

    lines.extend(["", "### Top 20 issues abertas", ""])
    if issue_rows:
        lines.extend(markdown_table(["Severidade", "Tipo", "Arquivo", "Linha", "Mensagem"], issue_rows))
    else:
        lines.append("Nenhuma issue aberta retornada pela API.")

    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Gera snapshot Markdown/JSON da API do SonarQube Cloud.")
    parser.add_argument("--project-key", default=os.environ.get("SONAR_PROJECT_KEY", ""))
    parser.add_argument("--organization-key", default=os.environ.get("SONAR_ORGANIZATION_KEY", ""))
    parser.add_argument("--output-dir", default=os.environ.get("SONAR_REPORT_DIR", "artifacts/sonarqube"))
    parser.add_argument("--pull-request", default=os.environ.get("SONAR_PULL_REQUEST", ""))
    parser.add_argument("--host-url", default=os.environ.get("SONAR_HOST_URL", "https://sonarcloud.io"))
    parser.add_argument("--token-env", default="SONAR_TOKEN")
    args = parser.parse_args()

    if not args.project_key:
        print("project key nao informado.", file=sys.stderr)
        return 2
    if not args.organization_key:
        print("organization key nao informado.", file=sys.stderr)
        return 2

    output_dir = pathlib.Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    quality_gate_path = output_dir / "quality-gate.json"
    measures_path = output_dir / "measures.json"
    issues_path = output_dir / "issues.json"
    report_path = output_dir / "sonarqube-cloud-report.md"
    report_alias_path = output_dir / "report.md"

    sonar_token = os.environ.get(args.token_env, "")
    pull_request_number = args.pull_request or get_pull_request_number(
        os.environ.get("GITHUB_EVENT_NAME", ""),
        os.environ.get("GITHUB_EVENT_PATH", ""),
    )
    sonar_context_query = {"pullRequest": pull_request_number} if pull_request_number else {}
    dashboard_query = {"id": args.project_key, **sonar_context_query}
    dashboard_url = args.host_url.rstrip("/") + "/project/overview?" + urllib.parse.urlencode(dashboard_query)

    try:
        if not sonar_token:
            message = f"{args.token_env} nao esta configurado; pulando consulta a API do SonarQube Cloud."
            write_json(quality_gate_path, {"error": message})
            write_json(measures_path, {"error": message})
            write_json(issues_path, {"error": message})
            report = build_report(args.project_key, args.organization_key, dashboard_url, pull_request_number, error=message)
            report_path.write_text(report, encoding="utf-8")
            report_alias_path.write_text(report, encoding="utf-8")
            print(message)
            return 0

        quality_gate = request_json(
            args.host_url,
            sonar_token,
            "qualitygates/project_status",
            {"projectKey": args.project_key, **sonar_context_query},
        )
        measures = request_json(
            args.host_url,
            sonar_token,
            "measures/component",
            {
                "component": args.project_key,
                "metricKeys": ",".join(METRIC_KEYS),
                **sonar_context_query,
            },
        )
        issues = request_json(
            args.host_url,
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

        write_json(quality_gate_path, quality_gate)
        write_json(measures_path, measures)
        write_json(issues_path, issues)

        report = build_report(args.project_key, args.organization_key, dashboard_url, pull_request_number, quality_gate, measures, issues)
        report_path.write_text(report, encoding="utf-8")
        report_alias_path.write_text(report, encoding="utf-8")
    except Exception as exc:
        message = f"{type(exc).__name__}: {exc}"
        write_json(quality_gate_path, {"error": message})
        write_json(measures_path, {"error": message})
        write_json(issues_path, {"error": message})
        report = build_report(args.project_key, args.organization_key, dashboard_url, pull_request_number, error=message)
        report_path.write_text(report, encoding="utf-8")
        report_alias_path.write_text(report, encoding="utf-8")
        print(f"SonarQube Cloud report could not be generated: {message}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
