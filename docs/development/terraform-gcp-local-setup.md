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

As tasks `init` e `validate` apontam para o modulo atualmente versionado em `infra/terraform/modules/pubsub-ledger-events`. Quando um root module de ambiente for criado, ajuste o `cwd` dessas tasks para o diretorio correspondente, por exemplo `infra/terraform/environments/dev`.

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

## Guardrails

- Nao execute `terraform apply` automaticamente.
- Nao execute `terraform destroy` automaticamente.
- Use `terraform plan` somente quando existir ambiente Terraform real e houver autorizacao explicita.
- Nao versione secrets, chaves JSON, arquivos `.tfvars` reais, `*.tfstate`, `.terraform/` ou planos binarios.
- Prefira impersonation ou Workload Identity Federation em vez de chaves JSON de service account.
- Nao dependa de projeto, conta ou regiao padrao para operacoes que possam afetar recursos reais.

O Google Cloud CLI esta disponivel para autenticacao e descoberta controlada. Este fluxo de validacao local nao executa login, habilitacao de APIs, alteracoes de IAM ou deploy.
