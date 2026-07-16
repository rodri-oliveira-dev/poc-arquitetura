import json
import pathlib
import subprocess
import sys
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
SCRIPT = REPO_ROOT / "scripts" / "security" / "enrich-zap-openapi.py"
EXAMPLES = REPO_ROOT / "scripts" / "security" / "owasp-zap-openapi-examples.json"


class EnrichZapOpenApiTests(unittest.TestCase):
    def test_enriches_parameters_and_request_body_examples(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            input_path = root / "openapi.json"
            output_path = root / "openapi-zap.json"
            examples_path = root / "examples.json"
            summary_path = root / "summary.txt"

            input_path.write_text(
                json.dumps(
                    {
                        "openapi": "3.0.4",
                        "paths": {
                            "/api/v1/items/{id}": {
                                "post": {
                                    "parameters": [
                                        {
                                            "name": "id",
                                            "in": "path",
                                            "required": True,
                                            "schema": {"type": "string", "format": "uuid"},
                                        },
                                        {
                                            "name": "Idempotency-Key",
                                            "in": "header",
                                            "schema": {"type": "string"},
                                        },
                                    ],
                                    "requestBody": {
                                        "content": {
                                            "application/json": {
                                                "schema": {"type": "object"}
                                            }
                                        }
                                    },
                                }
                            }
                        },
                    }
                ),
                encoding="utf-8",
            )
            examples_path.write_text(
                json.dumps(
                    {
                        "parameters": {
                            "Idempotency-Key": "idem-1"
                        },
                        "apis": {
                            "test-api": {
                                "operations": {
                                    "POST /api/v1/items/{id}": {
                                        "requestBody": {
                                            "name": "zap"
                                        }
                                    }
                                }
                            }
                        },
                    }
                ),
                encoding="utf-8",
            )

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--input",
                    str(input_path),
                    "--output",
                    str(output_path),
                    "--examples",
                    str(examples_path),
                    "--api",
                    "test-api",
                    "--summary-output",
                    str(summary_path),
                ],
                cwd=REPO_ROOT,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            enriched = json.loads(output_path.read_text(encoding="utf-8"))
            operation = enriched["paths"]["/api/v1/items/{id}"]["post"]
            self.assertEqual("11111111-1111-4111-8111-111111111111", operation["parameters"][0]["example"])
            self.assertEqual("idem-1", operation["parameters"][1]["example"])
            self.assertEqual({"name": "zap"}, operation["requestBody"]["content"]["application/json"]["example"])
            self.assertIn("OPENAPI_EXAMPLES_APPLIED=3", summary_path.read_text(encoding="utf-8"))

    def test_versioned_examples_use_authorized_local_merchants(self) -> None:
        payload = json.loads(EXAMPLES.read_text(encoding="utf-8"))
        serialized = json.dumps(payload)

        self.assertNotIn("merchant-zap", serialized)
        self.assertNotIn("merchant-origin-zap", serialized)
        self.assertNotIn("merchant-destination-zap", serialized)
        self.assertEqual("m1", payload["parameters"]["merchantId"])
        self.assertEqual(
            "m1",
            payload["apis"]["transfer-service-api"]["operations"]["POST /api/v1/transferencias"]["requestBody"]["sourceMerchantId"],
        )
        self.assertEqual(
            "m2",
            payload["apis"]["transfer-service-api"]["operations"]["POST /api/v1/transferencias"]["requestBody"]["destinationMerchantId"],
        )


if __name__ == "__main__":
    unittest.main()
