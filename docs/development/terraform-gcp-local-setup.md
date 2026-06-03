# Setup local Terraform e GCP no Windows

Este guia prepara o ambiente local para validar a infraestrutura Terraform da POC sem alterar recursos reais no Google Cloud.

## Ferramentas

No PowerShell, instale as ferramentas pelo Windows Package Manager:

```powershell
winget install --id Hashicorp.Terraform --exact
winget install --id Google.CloudSDK --exact
winget install --id TerraformLinters.tflint --exact
```

Os IDs acima foram validados no catalogo `winget`. Como alternativa, consulte as paginas oficiais de instalacao:

- Terraform CLI: https://developer.hashicorp.com/terraform/install
- Google Cloud CLI: https://cloud.google.com/sdk/docs/install
- TFLint: https://github.com/terraform-linters/tflint

Abra um novo terminal e confirme a instalacao:

```powershell
terraform version
gcloud version
tflint --version
```

## VS Code

O arquivo `.vscode/extensions.json` recomenda:

- `hashicorp.terraform`: suporte a Terraform e HCL;
- `hashicorp.hcl`: destaque de sintaxe para arquivos HCL adicionais;
- `googlecloudtools.cloudcode`: integracao do Google Cloud ao VS Code.

As tasks ficam em `.vscode/tasks.json`. Execute `Tasks: Run Task` e escolha:

- `terraform: fmt check`;
- `terraform: init without backend`;
- `terraform: validate`;
- `terraform: tflint`;
- `terraform: validate all`.

As tasks `init` e `validate` apontam para o root module de desenvolvimento em `infra/terraform/environments/dev`. Esse ambiente habilita `pubsub.googleapis.com`, compoe o modulo reutilizavel `infra/terraform/modules/pubsub-ledger-events` e configura backend remoto GCS parcial, com prefixo de state `poc-arquitetura/pubsub/dev`. O bucket real deve ser informado manualmente no `terraform init` operacional, conforme a [ADR-0080](../adrs/0080-backend-remoto-gcs-terraform-dev.md). O passo a passo para configurar variaveis locais, revisar o plano e aplicar manualmente fica em [`infra/terraform/environments/dev/README.md`](../../infra/terraform/environments/dev/README.md).

## Validacao local

Para validar todos os diretorios Terraform versionados no Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/validate-terraform.ps1
```

No Git Bash, Linux ou macOS:

```bash
./scripts/validate-terraform.sh
```

Os scripts executam apenas validacoes locais e nao destrutivas:

```bash
terraform fmt -check -recursive
terraform init -backend=false
terraform validate
tflint --recursive
```

O `terraform init -backend=false` e usado aqui somente para validacao sintatica
local sem credenciais e sem acesso ao bucket GCS. Esse modo nao exercita o
locking remoto e nao deve ser usado antes de `terraform plan`, `terraform
apply` ou `terraform destroy`. Para operacao real, inicialize o backend remoto:

```powershell
Set-Location ./infra/terraform/environments/dev
terraform init -backend-config="bucket=<terraform-state-bucket>"
terraform validate
```

O hook `.githooks/pre-push` executa a mesma validacao quando encontra arquivos `*.tf` versionados. A ausencia de `terraform` ou `tflint` falha com orientacao para instalar a ferramenta. A validacao sintatica basica nao depende de autenticacao GCP, secrets ou projeto real.

O mesmo hook tambem executa Trivy quando a ferramenta esta instalada localmente, cobrindo misconfigurations em Terraform e Dockerfiles, secrets e vulnerabilidades detectaveis no filesystem. A ausencia local do Trivy mostra apenas um aviso e nao bloqueia o push. Consulte [validacao de seguranca com Trivy](trivy-security-scan.md).

## Guardrails

- Nao execute `terraform apply` automaticamente.
- Nao execute `terraform destroy` automaticamente.
- Use `terraform plan` somente quando existir ambiente Terraform real e houver autorizacao explicita.
- Nao use `-lock=false` com backend remoto; o locking do backend GCS deve proteger concorrencia entre operadores.
- Nao versione secrets, chaves JSON, arquivos `.tfvars` reais, `*.tfstate`, `.terraform/` ou planos binarios.
- Prefira impersonation ou Workload Identity Federation em vez de chaves JSON de service account.
- Nao dependa de projeto, conta ou regiao padrao para operacoes que possam afetar recursos reais.

O Google Cloud CLI esta disponivel para autenticacao e descoberta controlada. Este fluxo de validacao local nao executa login, habilitacao de APIs, alteracoes de IAM ou deploy.

## Validacao no CI

O workflow `.github/workflows/terraform-validation.yml`, chamado `terraform-validation`, roda em pull requests e pushes para `main` que alteram Terraform, Dockerfiles, Compose ou o proprio workflow. Ele executa:

```bash
trivy config --severity HIGH,CRITICAL .
trivy fs --scanners vuln,secret,misconfig --severity HIGH,CRITICAL .
terraform fmt -check -recursive ./infra/terraform
tflint --chdir=./infra/terraform --recursive
```

Internamente, o workflow usa `scripts/validate-terraform.sh`, portanto tambem executa `terraform init -backend=false -input=false` e `terraform validate` em cada diretorio versionado que contem arquivos `*.tf`, incluindo o root module de desenvolvimento e modulos reutilizaveis. Esse `init` sem backend e intencional para validacao sem credenciais; como o workflow nao executa `plan`, ele nao contorna locking de uma operacao remota.

O CI instala Terraform e TFLint no runner, entao nao depende das ferramentas instaladas na maquina do desenvolvedor. O Trivy tambem roda por action propria no CI. Esses checks sao bloqueantes para achados `HIGH` e `CRITICAL` do Trivy e para falhas de formatacao, inicializacao sem backend, validacao Terraform ou TFLint.

Esse fluxo nao exige GitHub Actions secrets, repository variables, chave JSON, autenticacao GCP ou projeto real. O workflow nao executa `terraform plan`, `terraform apply`, `terraform destroy`, nao gera plano binario e nao publica credenciais. Se um fluxo futuro executar `terraform plan` real no CI, ele deve inicializar o backend remoto GCS com bucket informado por configuracao segura e nao deve usar `-lock=false`.
