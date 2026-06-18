#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

MESSAGING_PROVIDER=PubSub COMPOSE_OVERLAY_FILE="$ROOT_DIR/compose.pubsub.yaml" \
  "$ROOT_DIR/scripts/start-local-stack.sh"
