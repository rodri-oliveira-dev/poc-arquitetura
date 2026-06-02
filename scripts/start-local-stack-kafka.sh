#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

MESSAGING_PROVIDER=Kafka COMPOSE_OVERLAY_FILE="$ROOT_DIR/compose.kafka.yaml" \
  "$ROOT_DIR/scripts/start-local-stack.sh"
