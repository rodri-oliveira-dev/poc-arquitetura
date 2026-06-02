#!/usr/bin/env sh
set -eu

if command -v git >/dev/null 2>&1; then
  repo_root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
else
  repo_root="$(pwd)"
fi

terraform_root="${TERRAFORM_ROOT:-$repo_root/infra/terraform}"

if [ ! -d "$terraform_root" ]; then
  echo "==> terraform: diretorio infra/terraform ausente; validacao ignorada"
  exit 0
fi

if ! find "$terraform_root" -type f -name '*.tf' -print -quit | grep -q .; then
  echo "==> terraform: nenhum arquivo .tf encontrado; validacao ignorada"
  exit 0
fi

for tool in terraform tflint
do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "terraform: ferramenta obrigatoria ausente: $tool. Consulte docs/development/terraform-gcp-local-setup.md." >&2
    exit 1
  fi
done

if command -v mktemp >/dev/null 2>&1; then
  terraform_directories="$(mktemp "${TMPDIR:-/tmp}/terraform-directories.XXXXXX")"
else
  terraform_directories="${TMPDIR:-/tmp}/terraform-directories.$$"
  : >"$terraform_directories"
fi

trap 'rm -f "$terraform_directories"' EXIT

find "$terraform_root" -type f -name '*.tf' -exec dirname {} \; | sort -u >"$terraform_directories"

echo "==> terraform: verificando formatacao"
terraform -chdir="$terraform_root" fmt -check -recursive

while IFS= read -r terraform_directory
do
  [ -n "$terraform_directory" ] || continue

  echo "==> terraform: inicializando sem backend em $terraform_directory"
  terraform -chdir="$terraform_directory" init -backend=false -input=false

  echo "==> terraform: validando $terraform_directory"
  terraform -chdir="$terraform_directory" validate
done <"$terraform_directories"

echo "==> terraform: executando tflint recursivo"
tflint --chdir="$terraform_root" --recursive

echo "==> terraform: validacoes aprovadas"
