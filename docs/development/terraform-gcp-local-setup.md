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
- `terraform: init backend false`;
- `terraform: validate`;
- `terraform: tflint`;
- `terraform: validate all`.

As tasks `init` e `validate` apontam para o root module de desenvolvimento em `infra/terraform/environments/dev`. Esse ambiente habilita `pubsub.googleapis.com`, compoe o modulo reutilizavel `infra/terraform/modules/pubsub-ledger-events` e nao configura backend remoto. O passo a passo para configurar variaveis locais, revisar o plano e aplicar manualmente fica em [`infra/terraform/environments/dev/README.md`](../../infra/terraform/environments/dev/README.md).

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

O hook `.githooks/pre-push` executa a mesma validacao quando encontra arquivos `*.tf` versionados. A ausencia de `terraform` ou `tflint` falha com orientacao para instalar a ferramenta. A validacao sintatica basica nao depende de autenticacao GCP, secrets ou projeto real.

O mesmo hook tambem executa Trivy quando a ferramenta esta instalada localmente, cobrindo misconfigurations em Terraform e Dockerfiles, secrets e vulnerabilidades detectaveis no filesystem. A ausencia local do Trivy mostra apenas um aviso e nao bloqueia o push. Consulte [validacao de seguranca com Trivy](trivy-security-scan.md).

## Guardrails

- Nao execute `terraform apply` automaticamente.
- Nao execute `terraform destroy` automaticamente.
- Use `terraform plan` somente quando existir ambiente Terraform real e houver autorizacao explicita.
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
terraform -chdir=./infra/terraform/environments/dev init -backend=false -input=false
terraform -chdir=./infra/terraform/environments/dev validate
```

O job opcional `Plan Terraform Dev` executa `terraform plan` somente em pull requests internos quando as seguintes GitHub Actions repository variables estiverem configuradas:

| Variavel | Finalidade |
| --- | --- |
| `GCP_PROJECT_ID` | Projeto GCP de desenvolvimento usado pelo root module. |
| `GCP_REGION` | Regiao GCP; quando ausente, o workflow usa `us-central1`. |
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | Identificador completo do provider de Workload Identity Federation. |
| `GCP_TERRAFORM_SERVICE_ACCOUNT` | E-mail da service account usada apenas para planejar a infraestrutura. |

O fluxo recomendado nao exige GitHub Actions secrets nem chave JSON. A service account deve confiar no repositorio via Workload Identity Federation, com `roles/iam.workloadIdentityUser` concedido somente ao principal federado esperado. Restrinja a federacao ao repositorio e aos refs aceitos pela politica do ambiente dev.

Para um plano real, conceda somente leitura suficiente para atualizar o estado conhecido pelo provider, revisando o menor privilegio no projeto dev. O baseline esperado e:

- `roles/browser`;
- `roles/serviceusage.serviceUsageViewer`;
- `roles/pubsub.viewer`;
- `roles/iam.serviceAccountViewer`.

Essas permissoes sao para `plan`. Qualquer permissao de escrita necessaria para um `apply` manual deve ser tratada separadamente e nao e usada pelo workflow.

Pull requests de forks nao recebem autenticacao GCP e nao executam o plano. O workflow nao executa `terraform apply`, nao gera plano binario e nao publica credenciais.
