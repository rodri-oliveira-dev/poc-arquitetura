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

As tasks `init` e `validate` apontam para o root module de desenvolvimento em `infra/terraform/environments/dev`. Esse ambiente habilita `pubsub.googleapis.com` e `sqladmin.googleapis.com`, compoe os modulos reutilizaveis `infra/terraform/modules/pubsub-ledger-events` e `infra/terraform/modules/cloudsql-postgres`, e configura backend remoto GCS parcial, com bucket `rodri-terraform-state-bucket` e prefixo de state `poc-arquitetura/pubsub/dev`. O prefixo foi mantido para compatibilidade com o state remoto existente, mas o mesmo root module agora compartilha ciclo de vida de Pub/Sub e Cloud SQL no ambiente dev. O passo a passo para configurar variaveis locais, revisar o plano e aplicar manualmente fica em [`infra/terraform/environments/dev/README.md`](../../infra/terraform/environments/dev/README.md).

## Validacao local

Para validar todos os diretorios Terraform versionados no Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/quality/terraform/validate.ps1
```

No Git Bash, Linux ou macOS:

```bash
./scripts/quality/terraform/validate.sh
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
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform validate
```

O hook `.githooks/pre-push` executa apenas `terraform fmt -check -recursive ./infra/terraform` quando encontra alteracoes em `*.tf` ou `*.tfvars`. Se a Terraform CLI nao estiver disponivel, o hook avisa e permite o push; a validacao completa fica no Pull Request.

O hook nao executa Trivy automaticamente. Para feedback antecipado, rode Trivy manualmente conforme [validacao de seguranca com Trivy](trivy-security-scan.md). O workflow de infraestrutura continua executando Trivy no Pull Request quando ha mudancas cobertas pelos filtros.

Para evitar falsos positivos fora do codigo versionado, os scans do Trivy ignoram diretorios gerados, dependencias locais e caches como `node_modules`, `dist`, `bin`, `obj`, `TestResults`, `coverage` e `.terraform`.

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

O workflow `.github/workflows/terraform-validation.yml`, chamado `terraform-validation`, roda em pull requests e pushes para `main` que alteram Terraform, scripts de validacao Terraform ou o proprio workflow. Ele executa:

```bash
terraform fmt -check -recursive ./infra/terraform
tflint --chdir=./infra/terraform --recursive
```

Internamente, o workflow usa `scripts/quality/terraform/validate.sh`, portanto tambem executa `terraform init -backend=false -input=false` e `terraform validate` em cada diretorio versionado que contem arquivos `*.tf`, incluindo o root module de desenvolvimento e modulos reutilizaveis. Esse `init` sem backend e intencional para validacao sem credenciais; como o workflow nao executa `plan`, ele nao contorna locking de uma operacao remota.

O CI instala Terraform e TFLint no runner, entao nao depende das ferramentas instaladas na maquina do desenvolvedor. Esses checks sao bloqueantes para falhas de formatacao, inicializacao sem backend, validacao Terraform ou TFLint.

O Trivy roda no workflow independente `.github/workflows/infrastructure-security.yml`, chamado `infrastructure-security`, quando ha mudancas em Terraform, Dockerfiles, Compose, na composite action do Trivy ou no proprio workflow. Esse workflow nao instala Terraform/TFLint e e bloqueante para achados `HIGH` e `CRITICAL`.

Esse fluxo nao exige GitHub Actions secrets, repository variables, chave JSON, autenticacao GCP ou projeto real. O workflow nao executa `terraform plan`, `terraform apply`, `terraform destroy`, nao gera plano binario e nao publica credenciais. Se um fluxo futuro executar `terraform plan` real no CI, ele deve inicializar o backend remoto GCS com bucket informado por configuracao segura e nao deve usar `-lock=false`.
