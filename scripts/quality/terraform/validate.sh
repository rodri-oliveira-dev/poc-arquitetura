#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/lib"
if [[ ! -f "$SCRIPTS_LIB_DIR/common.sh" ]]; then
  SCRIPTS_LIB_DIR="$SCRIPT_DIR/../../lib"
fi
# shellcheck source=../../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"

TERRAFORM_ROOT="${TERRAFORM_ROOT:-$ROOT_DIR/infra/terraform}"

if [[ ! -d "$TERRAFORM_ROOT" ]]; then
  echo "==> terraform: diretorio infra/terraform ausente; validacao ignorada"
  exit 0
fi

if ! find "$TERRAFORM_ROOT" -type f -name '*.tf' -print -quit | grep -q .; then
  echo "==> terraform: nenhum arquivo .tf encontrado; validacao ignorada"
  exit 0
fi

for tool in terraform tflint; do
  require_command "$tool" "terraform: ferramenta obrigatoria ausente: $tool. Consulte docs/development/terraform-gcp-local-setup.md."
done

if command -v mktemp >/dev/null 2>&1; then
  terraform_directories="$(mktemp "${TMPDIR:-/tmp}/terraform-directories.XXXXXX")"
else
  terraform_directories="${TMPDIR:-/tmp}/terraform-directories.$$"
  : >"$terraform_directories"
fi

trap 'rm -f "$terraform_directories"' EXIT

find "$TERRAFORM_ROOT" -type f -name '*.tf' -exec dirname {} \; | sort -u >"$terraform_directories"

echo "==> terraform: verificando formatacao"
terraform -chdir="$TERRAFORM_ROOT" fmt -check -recursive

while IFS= read -r terraform_directory; do
  [ -n "$terraform_directory" ] || continue

  echo "==> terraform: inicializando sem backend para validacao sintatica em $terraform_directory"
  terraform -chdir="$terraform_directory" init -backend=false -input=false

  echo "==> terraform: validando $terraform_directory"
  terraform -chdir="$terraform_directory" validate
done <"$terraform_directories"

echo "==> terraform: executando tflint recursivo"
tflint --chdir="$TERRAFORM_ROOT" --recursive

echo "==> terraform: validacoes aprovadas"
