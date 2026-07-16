import os
import pathlib
import shutil
import subprocess
import tempfile
import textwrap
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
SCRIPT = REPO_ROOT / "scripts" / "security" / "run-owasp-zap-local.sh"


class RunOwaspZapLocalTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = pathlib.Path(tempfile.mkdtemp())
        self.bin_dir = self.temp_dir / "bin"
        self.bin_dir.mkdir()
        self.output_root = self.temp_dir / "reports"
        self.real_bash = self.find_bash()
        self.env_file = self.temp_dir / "provided.env"
        self.env_file.write_text(self.env_content(), encoding="utf-8")
        self.bash_env = self.temp_dir / "bash_env.sh"
        self.bash_env.write_text(self.fake_bash_function(), encoding="utf-8", newline="\n")
        self.write_executable(self.bin_dir / "docker", self.fake_docker())
        self.write_executable(self.bin_dir / "dotnet", self.fake_dotnet())
        self.write_executable(self.bin_dir / "curl", "#!/usr/bin/bash\nexit ${FAKE_CURL_EXIT:-0}\n")
        self.write_executable(self.bin_dir / "python3", "#!/usr/bin/bash\npython \"$@\"\n")
        self.write_executable(self.bin_dir / "sleep", "#!/usr/bin/bash\nexit 0\n")

    def tearDown(self) -> None:
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def find_bash(self) -> str:
        for candidate in (
            pathlib.Path(r"C:\Program Files\Git\bin\bash.exe"),
            pathlib.Path(r"C:\Program Files\Git\usr\bin\bash.exe"),
        ):
            if candidate.is_file():
                return str(candidate)
        return shutil.which("bash") or "bash"

    def write_executable(self, path: pathlib.Path, content: str) -> None:
        path.write_text(textwrap.dedent(content).lstrip(), encoding="utf-8", newline="\n")
        path.chmod(0o755)

    def to_bash_path(self, path: pathlib.Path | str) -> str:
        value = str(path).replace("\\", "/")
        if len(value) >= 3 and value[1] == ":" and value[2] == "/":
            return f"/{value[0].lower()}{value[2:]}"
        return value

    def env_content(self) -> str:
        return textwrap.dedent(
            """
            POSTGRES_HOST_PORT=15432
            LEDGER_DB_MIGRATOR_PASSWORD=ledger-secret
            BALANCE_DB_MIGRATOR_PASSWORD=balance-secret
            TRANSFER_DB_MIGRATOR_PASSWORD=transfer-secret
            PAYMENT_DB_MIGRATOR_PASSWORD=payment-secret
            AUDIT_DB_MIGRATOR_PASSWORD=audit-secret
            IDENTITY_DB_MIGRATOR_PASSWORD=identity-secret
            KEYCLOAK_CLIENT_SECRET=client-secret
            LEDGER_SERVICE_HOST_PORT=5226
            BALANCE_SERVICE_HOST_PORT=5228
            TRANSFER_SERVICE_HOST_PORT=5230
            IDENTITY_SERVICE_HOST_PORT=5232
            PAYMENT_SERVICE_HOST_PORT=5234
            AUDIT_SERVICE_HOST_PORT=5235
            """
        ).lstrip()

    def fake_bash_function(self) -> str:
        create_env = (REPO_ROOT / "scripts" / "local" / "create-env-local.sh").as_posix()
        get_token = (REPO_ROOT / "scripts" / "validation" / "get-token.sh").as_posix()
        preflight = (REPO_ROOT / "scripts" / "security" / "validate-zap-authentication.sh").as_posix()
        zap_all = (REPO_ROOT / "scripts" / "security" / "run-owasp-zap-all-apis.sh").as_posix()
        env_payload = self.env_content().replace("'", "'\"'\"'")
        preflight_log = self.to_bash_path(self.temp_dir / "preflight-args.log")
        zap_log = self.to_bash_path(self.temp_dir / "zap-args.log")
        docker_log = self.to_bash_path(self.temp_dir / "docker.log")
        dotnet_log = self.to_bash_path(self.temp_dir / "dotnet.log")
        return textwrap.dedent(
            f"""
            docker() {{
              printf 'docker' >> '{docker_log}'
              local arg
              for arg in "$@"; do printf ' %q' "$arg" >> '{docker_log}'; done
              printf '\\n' >> '{docker_log}'

              if [[ "${{FAKE_DOCKER_FAIL:-}}" == "config" && "$*" == *" config --quiet"* ]]; then return 35; fi
              if [[ "${{FAKE_DOCKER_FAIL:-}}" == "down" && "$*" == *" down --volumes --remove-orphans"* ]]; then return 36; fi

              case "${{1:-}}" in
                version) return 0 ;;
                compose)
                  case "$*" in
                    *" ps -q "*) echo "container-id"; return 0 ;;
                    *" exec -T postgres-db "*) return 0 ;;
                    *) return 0 ;;
                  esac
                  ;;
                inspect)
                  echo "project_poc-net"
                  return 0
                  ;;
              esac
              return 0
            }}

            dotnet() {{
              printf 'dotnet' >> '{dotnet_log}'
              local arg
              for arg in "$@"; do printf ' %q' "$arg" >> '{dotnet_log}'; done
              printf '\\n' >> '{dotnet_log}'
              if [[ "${{FAKE_MIGRATION_FAIL:-}}" == "true" ]]; then return 44; fi
              return 0
            }}

            curl() {{
              return "${{FAKE_CURL_EXIT:-0}}"
            }}

            sleep() {{
              return 0
            }}

            python3() {{
              python "$@"
            }}

            bash() {{
              local script="${{1:-}}"
              shift || true
              case "$script" in
                '{create_env}')
                  if [[ "${{FAKE_CREATE_ENV_FAIL:-}}" == "true" ]]; then return 31; fi
                  output=""
                  while [[ $# -gt 0 ]]; do
                    if [[ "$1" == "--output" ]]; then output="$2"; shift 2; else shift; fi
                  done
                  printf '%s' '{env_payload}' > "$output"
                  return 0
                  ;;
                '{get_token}')
                  printf '%s' 'local-secret-token'
                  return 0
                  ;;
                '{preflight}')
                  printf '%s\\n' "$script $*" >> "{preflight_log}"
                  if [[ "${{FAKE_PREFLIGHT_FAIL:-}}" == "true" ]]; then return 41; fi
                  return 0
                  ;;
                '{zap_all}')
                  printf '%s\\n' "$script $*" >> "{zap_log}"
                  output_root=""
                  while [[ $# -gt 0 ]]; do
                    if [[ "$1" == "--output-root" ]]; then output_root="$2"; shift 2; else shift; fi
                  done
                  mkdir -p "$output_root/20260101-010101"
                  printf '# summary\\n' > "$output_root/20260101-010101/summary.md"
                  printf '{{}}\\n' > "$output_root/20260101-010101/ledger-service-api.json"
                  return "${{FAKE_ZAP_EXIT:-0}}"
                  ;;
              esac
              "$REAL_BASH" "$script" "$@"
            }}
            """
        ).strip()

    def fake_docker(self) -> str:
        log = self.to_bash_path(self.temp_dir / "docker.log")
        return f"""
            #!/usr/bin/bash
            set -euo pipefail
            printf 'docker' >> '{log}'
            for arg in "$@"; do printf ' %q' "$arg" >> '{log}'; done
            printf '\\n' >> '{log}'

            if [[ "${{FAKE_DOCKER_FAIL:-}}" == "config" && "$*" == *" config --quiet"* ]]; then exit 35; fi
            if [[ "${{FAKE_DOCKER_FAIL:-}}" == "down" && "$*" == *" down --volumes --remove-orphans"* ]]; then exit 36; fi

            case "${{1:-}}" in
              version) exit 0 ;;
              compose)
                case "$*" in
                  *" ps -q "*) echo "container-id"; exit 0 ;;
                  *" exec -T postgres-db "*) exit 0 ;;
                  *) exit 0 ;;
                esac
                ;;
              inspect)
                echo "project_poc-net"
                exit 0
                ;;
            esac
            exit 0
        """

    def fake_dotnet(self) -> str:
        log = self.to_bash_path(self.temp_dir / "dotnet.log")
        return f"""
            #!/usr/bin/bash
            set -euo pipefail
            printf 'dotnet' >> '{log}'
            for arg in "$@"; do printf ' %q' "$arg" >> '{log}'; done
            printf '\\n' >> '{log}'
            if [[ "${{FAKE_MIGRATION_FAIL:-}}" == "true" ]]; then exit 44; fi
            exit 0
        """

    def run_script(self, *args: str, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
        converted_args = [self.to_bash_path(arg) if ":\\" in arg else arg for arg in args]
        run_env = os.environ.copy()
        run_env.update(
            {
                "PATH": f"{self.to_bash_path(self.bin_dir)}:{run_env.get('PATH', '')}",
                "REAL_BASH": self.real_bash,
                "BASH_ENV": self.to_bash_path(self.bash_env),
            }
        )
        if env:
            run_env.update(env)
        return subprocess.run(
            [self.real_bash, str(SCRIPT), "--output-root", self.to_bash_path(self.output_root), *converted_args],
            cwd=REPO_ROOT,
            env=run_env,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )

    def docker_log(self) -> str:
        return (self.temp_dir / "docker.log").read_text(encoding="utf-8") if (self.temp_dir / "docker.log").exists() else ""

    def test_default_flow_runs_preflight_zap_and_cleanup(self) -> None:
        result = self.run_script("--env-file", str(self.env_file))

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("[owasp-local] Preparando ambiente", result.stdout)
        self.assertIn("[owasp-local] Executando OWASP ZAP", result.stdout)
        self.assertIn("down --volumes --remove-orphans", self.docker_log())

    def test_active_scan_is_forwarded_and_announced(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), "--active-scan")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Active scan habilitado", result.stdout)
        self.assertIn("--active-scan", (self.temp_dir / "zap-args.log").read_text(encoding="utf-8"))

    def test_fail_on_alerts_is_forwarded(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), "--fail-on-alerts")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("--fail-on-alerts", (self.temp_dir / "zap-args.log").read_text(encoding="utf-8"))

    def test_keep_environment_skips_down(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), "--keep-environment")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertNotIn("down --volumes --remove-orphans", self.docker_log())
        self.assertIn("Ambiente preservado", result.stderr)

    def test_skip_build_does_not_add_build_flag(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), "--skip-build")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertNotIn("--build", self.docker_log())

    def test_environment_creation_failure_returns_original_code(self) -> None:
        result = self.run_script(env={"FAKE_CREATE_ENV_FAIL": "true"})

        self.assertEqual(31, result.returncode)

    def test_migration_failure_runs_cleanup(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), env={"FAKE_MIGRATION_FAIL": "true"})

        self.assertEqual(44, result.returncode)
        self.assertIn("down --volumes --remove-orphans", self.docker_log())

    def test_api_unavailable_runs_cleanup(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), env={"FAKE_CURL_EXIT": "7"})

        self.assertNotEqual(0, result.returncode)
        self.assertIn("API indisponivel", result.stderr)
        self.assertIn("down --volumes --remove-orphans", self.docker_log())

    def test_preflight_failure_returns_original_code(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), env={"FAKE_PREFLIGHT_FAIL": "true"})

        self.assertEqual(41, result.returncode)
        self.assertIn("down --volumes --remove-orphans", self.docker_log())

    def test_zap_failure_returns_original_code_and_preserves_reports(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), env={"FAKE_ZAP_EXIT": "7"})

        self.assertEqual(7, result.returncode)
        self.assertTrue((self.output_root / "20260101-010101" / "summary.md").is_file())

    def test_cleanup_failure_after_success_is_returned(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), env={"FAKE_DOCKER_FAIL": "down"})

        self.assertEqual(36, result.returncode)

    def test_cleanup_failure_does_not_hide_original_error(self) -> None:
        result = self.run_script(
            "--env-file",
            str(self.env_file),
            env={"FAKE_ZAP_EXIT": "9", "FAKE_DOCKER_FAIL": "down"},
        )

        self.assertEqual(9, result.returncode)

    def test_does_not_delete_explicit_env_file(self) -> None:
        result = self.run_script("--env-file", str(self.env_file))

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(self.env_file.exists())

    def test_deletes_temporary_env_file(self) -> None:
        result = self.run_script()

        self.assertEqual(0, result.returncode, result.stderr)
        temp_env_dir = REPO_ROOT / "artifacts" / "zap-local-env"
        leftovers = list(temp_env_dir.glob(".env.owasp-zap-local.*")) if temp_env_dir.exists() else []
        self.assertEqual([], leftovers)

    def test_does_not_expose_secrets(self) -> None:
        result = self.run_script("--env-file", str(self.env_file), env={"FAKE_ZAP_EXIT": "5"})

        combined = result.stdout + result.stderr + self.docker_log()
        self.assertNotIn("local-secret-token", combined)
        self.assertNotIn("client-secret", combined)


if __name__ == "__main__":
    unittest.main()
