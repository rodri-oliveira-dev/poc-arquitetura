#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

MESSAGING_PROVIDER=Kafka "$ROOT_DIR/scripts/local/start-stack.sh"

