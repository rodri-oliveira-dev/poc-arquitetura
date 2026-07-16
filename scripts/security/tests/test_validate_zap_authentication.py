import os
import pathlib
import shutil
import subprocess
import tempfile
import textwrap
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
SCRIPT = REPO_ROOT / "scripts" / "security" / "validate-zap-authentication.sh"


class ValidateZapAuthenticationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = pathlib.Path(tempfile.mkdtemp())
        self.bin_dir = self.temp_dir / "bin"
        self.bin_dir.mkdir()
        self.token = "super-secret-token"
        self.real_bash = self.find_bash()
        self.curl_function: str | None = None

    def tearDown(self) -> None:
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def find_bash(self) -> str:
        candidates = [
            pathlib.Path(r"C:\Program Files\Git\bin\bash.exe"),
            pathlib.Path(r"C:\Program Files\Git\usr\bin\bash.exe"),
        ]
        for candidate in candidates:
            if candidate.is_file():
                return str(candidate)

        return shutil.which("bash") or "bash"

    def write_executable(self, path: pathlib.Path, content: str) -> None:
        path.write_text(textwrap.dedent(content).lstrip(), encoding="utf-8", newline="\n")
        path.chmod(0o755)

    def write_fake_curl(self, statuses: list[str], assert_token: str | None = None) -> pathlib.Path:
        status_file = self.temp_dir / "statuses.txt"
        status_file.write_text("\n".join(statuses) + "\n", encoding="utf-8")
        calls_file = self.temp_dir / "curl-calls.txt"
        token_assertion = ""
        if assert_token is not None:
            token_assertion = f"""
            expected='Authorization: Bearer {assert_token}'
            found=false
            for arg in "$@"; do
              if [[ "$arg" == "$expected" ]]; then
                found=true
              fi
            done
            if [[ "$found" != true ]]; then
              echo "Authorization header missing" >&2
              exit 97
            fi
            """

        self.curl_function = textwrap.dedent(
            f"""
            () {{
              calls_file='{calls_file.as_posix()}'
              status_file='{status_file.as_posix()}'
              printf '%s\\n' "$*" >> "$calls_file"
              {token_assertion}
              status="$(head -n 1 "$status_file")"
              tail -n +2 "$status_file" > "$status_file.tmp"
              mv "$status_file.tmp" "$status_file"
              if [[ "$status" == "CURL_FAIL" ]]; then
                return 7
              fi
              printf '%s' "$status"
            }}
            """
        ).strip()
        return calls_file

    def fake_bash_function_for_token_script(self, token: str = "generated-token", exit_code: int = 0) -> str:
        token_script = (REPO_ROOT / "scripts" / "validation" / "get-token.sh").as_posix()
        if exit_code == 0:
            return f"() {{ if [[ \"${{1:-}}\" == '{token_script}' ]]; then printf '%s' '{token}'; return 0; fi; return 127; }}"
        return f"() {{ if [[ \"${{1:-}}\" == '{token_script}' ]]; then return {exit_code}; fi; return 127; }}"

    def run_script(self, *args: str, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
        run_env = os.environ.copy()
        run_env.update(
            {
                "PATH": f"{self.bin_dir}{os.pathsep}{run_env.get('PATH', '')}",
                "LEDGER_API_URL": "http://ledger.test",
                "BALANCE_API_URL": "http://balance.test",
                "TRANSFER_API_URL": "http://transfer.test",
                "PAYMENT_API_URL": "http://payment.test",
                "AUDIT_API_URL": "http://audit.test",
                "IDENTITY_API_URL": "http://identity.test",
            }
        )
        if env:
            run_env.update(env)
        if self.curl_function is not None:
            run_env["BASH_FUNC_curl%%"] = self.curl_function

        return subprocess.run(
            [self.real_bash, str(SCRIPT), *args],
            cwd=REPO_ROOT,
            env=run_env,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )

    def test_rejects_empty_token_argument(self) -> None:
        result = self.run_script("--token", "")

        self.assertEqual(2, result.returncode)
        self.assertIn("--token", result.stderr)

    def test_uses_token_argument(self) -> None:
        self.write_fake_curl(["200"] * 6, assert_token=self.token)

        result = self.run_script("--token", self.token)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Todas as APIs aceitaram o token", result.stdout)

    def test_obtains_token_with_existing_script_contract(self) -> None:
        self.write_fake_curl(["200"] * 6, assert_token="generated-token")
        env_file = self.temp_dir / "local.env"
        env_file.write_text("KEY=value\n", encoding="utf-8")

        result = self.run_script(
            "--env-file",
            str(env_file),
            env={
                "BASH_FUNC_bash%%": self.fake_bash_function_for_token_script("generated-token"),
            },
        )

        self.assertEqual(0, result.returncode, result.stderr)

    def test_all_apis_accept_token(self) -> None:
        self.write_fake_curl(["200", "204", "400", "404", "409", "422"])

        result = self.run_script("--token", self.token)

        self.assertEqual(0, result.returncode, result.stderr)

    def test_fails_when_api_returns_401(self) -> None:
        self.write_fake_curl(["200", "200", "401", "200", "200", "200"])

        result = self.run_script("--token", self.token)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("TransferService.Api", result.stderr)
        self.assertIn("HTTP 401", result.stderr)

    def test_fails_when_api_returns_403(self) -> None:
        self.write_fake_curl(["200", "403", "200", "200", "200", "200"])

        result = self.run_script("--token", self.token)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("BalanceService.Api", result.stderr)
        self.assertIn("HTTP 403", result.stderr)

    def test_accepts_404(self) -> None:
        self.write_fake_curl(["404"] * 6)

        result = self.run_script("--token", self.token)

        self.assertEqual(0, result.returncode, result.stderr)

    def test_accepts_400(self) -> None:
        self.write_fake_curl(["400"] * 6)

        result = self.run_script("--token", self.token)

        self.assertEqual(0, result.returncode, result.stderr)

    def test_fails_on_connection_error(self) -> None:
        self.write_fake_curl(["CURL_FAIL", "200", "200", "200", "200", "200"])

        result = self.run_script("--token", self.token)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("HTTP 000", result.stderr)

    def test_fails_on_500(self) -> None:
        self.write_fake_curl(["200", "200", "200", "500", "200", "200"])

        result = self.run_script("--token", self.token)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("PaymentService.Api", result.stderr)
        self.assertIn("HTTP 500", result.stderr)

    def test_does_not_print_token(self) -> None:
        self.write_fake_curl(["401"] * 6)

        result = self.run_script("--token", self.token)

        combined_output = result.stdout + result.stderr
        self.assertNotIn(self.token, combined_output)

    def test_invalid_argument(self) -> None:
        result = self.run_script("--unknown")

        self.assertEqual(2, result.returncode)
        self.assertIn("Opcao invalida", result.stderr)

    def test_help(self) -> None:
        result = self.run_script("--help")

        self.assertEqual(0, result.returncode)
        self.assertIn("validate-zap-authentication.sh", result.stderr)


if __name__ == "__main__":
    unittest.main()
