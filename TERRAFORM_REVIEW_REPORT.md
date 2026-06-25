# Revisao Tecnica Terraform

## Resumo executivo

O Terraform versionado esta concentrado em um root module de desenvolvimento em
`infra/terraform/environments/dev` e em um modulo reutilizavel de Pub/Sub em
`infra/terraform/modules/pubsub-ledger-events`. A estrutura esta pequena,
legivel e coerente com a decisao atual da POC: provisionar recursos reais de
Pub/Sub em GCP apenas de forma manual e controlada, mantendo o emulator local
fora do Terraform.

As validacoes locais nao destrutivas passaram: `terraform fmt`, `terraform
init -backend=false`, `terraform validate` e `tflint --recursive`. Nao foram
encontrados secrets versionados nos arquivos Terraform analisados. O arquivo
local `terraform.tfvars` existe no workspace com projeto e e-mail reais, mas
esta ignorado pelo Git e nao aparece em `git ls-files`.

Principais riscos:

- Uso atual de state local no ambiente dev, aceitavel para POC, mas arriscado
  quando houver colaboracao ou ambientes persistentes.
- Job opcional de plan no GitHub Actions usa `-lock=false`, coerente com ausencia
  de backend remoto, mas perigoso se for reaproveitado em um backend com locking.
- CI ainda nao executa scanner de seguranca IaC, como Checkov, tfsec ou
  Terrascan.
- Existem artefatos locais ignorados (`terraform.tfstate`, `.terraform/` e
  `terraform.tfvars`) no workspace; nao estao versionados, mas exigem higiene
  operacional.

Principais melhorias recomendadas:

- Planejar backend remoto separado por ambiente antes de qualquer ambiente
  compartilhado ou persistente.
- Adicionar scanner IaC informativo em PR.
- Formalizar a politica de lockfile: manter lockfile do root module e nao
  depender de lockfile local de modulo reutilizavel.
- Tratar `-lock=false` como excecao documentada para o dev local sem backend.

## Achados por severidade

### Critico

Nenhum achado critico identificado.

### Alto

#### State local em ambiente que provisiona recursos reais

- Severidade: Alto
- Arquivo e linha aproximada: `infra/terraform/environments/dev/README.md:63`
- Descricao: a documentacao declara que nao ha backend remoto e que o Terraform
  usa state local por padrao. Isso e coerente com a POC, mas o mesmo root module
  habilita a API Pub/Sub e provisiona recursos reais em GCP.
- Impacto pratico: em uso compartilhado, duas pessoas podem divergir state,
  perder historico de alteracoes, sobrescrever recursos ou planejar contra uma
  visao incompleta da infraestrutura.
- Recomendacao: antes de promover esse ambiente para uso compartilhado,
  configurar backend remoto com separacao por ambiente, por exemplo bucket GCS
  dedicado por projeto/ambiente ou prefixo isolado.
- Exemplo de correcao:

```hcl
terraform {
  backend "gcs" {
    bucket = "poc-ledger-terraform-state-dev"
    prefix = "pubsub/dev"
  }
}
```

Use essa mudanca apenas com revisao explicita, pois altera a estrategia de
state e inicializacao.

### Medio

#### Plan no CI executa com lock desabilitado

- Severidade: Medio
- Arquivo e linha aproximada: `.github/workflows/terraform-validation.yml:83`
- Descricao: o job opcional `Plan Terraform Dev` usa `terraform plan -lock=false`.
  Com state local e `init -backend=false`, isso evita dependencia de backend,
  mas a flag se torna perigosa se o workflow evoluir para backend remoto.
- Impacto pratico: em backend remoto com locking, o plan poderia ignorar uma
  protecao importante contra concorrencia.
- Recomendacao: manter a flag apenas enquanto o workflow nao usar backend
  remoto. Ao adotar backend, remover `-lock=false` e validar o locking do
  backend.

#### Ausencia de scanner de seguranca IaC no pipeline Terraform

- Severidade: Medio
- Arquivo e linha aproximada: `.github/workflows/terraform-validation.yml:37`
- Descricao: o workflow executa `fmt`, `init` e `validate`; a validacao local
  tambem executa TFLint. Nao ha etapa de Checkov, tfsec, Terrascan ou ferramenta
  equivalente no pipeline Terraform.
- Impacto pratico: problemas de seguranca de IaC podem passar sem alerta em PR,
  especialmente IAM amplo, exposicao publica ou configuracoes inseguras quando
  novos recursos forem adicionados.
- Recomendacao: adicionar scanner inicialmente informativo ou com baseline
  revisado, para evitar ruido excessivo em uma POC.
- Exemplo de correcao:

```yaml
- name: Run Checkov
  uses: bridgecrewio/checkov-action@v12
  with:
    directory: infra/terraform
    quiet: true
```

#### TFLint existe localmente, mas nao aparece no workflow Terraform

- Severidade: Medio
- Arquivo e linha aproximada: `.github/workflows/terraform-validation.yml:37`
- Descricao: os scripts locais (`scripts/quality/terraform/validate.ps1` e
  `scripts/quality/terraform/validate.sh`) executam `tflint --recursive`, mas o workflow
  `terraform-validation` nao executa TFLint.
- Impacto pratico: um PR pode passar no GitHub Actions mesmo com problema que
  seria detectado pelo hook/pre-push local.
- Recomendacao: adicionar TFLint no workflow para alinhar CI e validacao local.

#### Lockfile do modulo existe localmente, mas esta ignorado

- Severidade: Medio
- Arquivo e linha aproximada: `.gitignore:109` e
  `infra/terraform/modules/pubsub-ledger-events/.terraform.lock.hcl`
- Descricao: `.gitignore` ignora lockfiles sob `infra/terraform/modules/**`.
  O lockfile do root dev esta versionado, mas o lockfile local do modulo nao.
  Isso e aceitavel para um modulo reutilizavel, desde que o time nao espere que
  esse lockfile controle versoes em CI.
- Impacto pratico: validar o modulo isoladamente pode baixar versoes permitidas
  pelas constraints, enquanto o root module usa o lockfile versionado. Pode
  gerar confusao sobre qual lockfile e fonte de verdade.
- Recomendacao: documentar que apenas root modules devem ter lockfile
  versionado. Opcionalmente remover o lockfile local ignorado do workspace em
  uma limpeza operacional, sem impacto no repositorio.

### Baixo

#### Arquivos locais ignorados contem valores reais de ambiente

- Severidade: Baixo
- Arquivo e linha aproximada: `infra/terraform/environments/dev/terraform.tfvars`
- Descricao: existe um `terraform.tfvars` local com `project_id` e membro IAM
  reais. O valor foi verificado apenas de forma mascarada neste relatorio, e o
  arquivo nao esta versionado.
- Impacto pratico: risco baixo no Git, mas alto se o arquivo for copiado para
  artefatos, tickets ou logs. O membro de smoke test concede
  `roles/iam.serviceAccountTokenCreator` nas duas service accounts dedicadas
  quando aplicado.
- Recomendacao: manter o arquivo local fora do Git, limpar
  `service_account_token_creator_members` depois de smoke tests e evitar anexar
  `terraform.tfvars` em qualquer canal externo.

#### Labels minimos podem ficar insuficientes ao expandir ambientes

- Severidade: Baixo
- Arquivo e linha aproximada:
  `infra/terraform/modules/pubsub-ledger-events/main.tf:18`
- Descricao: o modulo compoe labels com `app`, `environment` e `region`, alem de
  `managed_by` no root dev. Para a POC isso esta adequado; para ambientes reais,
  faltam labels de ownership, custo e criticidade.
- Impacto pratico: menor rastreabilidade de custo, operacao e auditoria quando
  novos recursos forem adicionados.
- Recomendacao: definir um contrato minimo de labels por ambiente antes de
  expandir a infraestrutura.

#### Outputs expoem identificadores internos, mas nao secrets

- Severidade: Baixo
- Arquivo e linha aproximada: `infra/terraform/environments/dev/outputs.tf:1`
- Descricao: os outputs incluem nomes, IDs e e-mails de service accounts. Esses
  valores ajudam a configurar os workers e nao sao secrets, mas revelam a
  topologia do ambiente.
- Impacto pratico: baixo, desde que outputs nao sejam publicados em logs
  externos ou artefatos publicos.
- Recomendacao: manter outputs operacionais apenas quando consumidos pela
  aplicacao ou checklist. Se novos outputs sensiveis forem adicionados, marcar
  `sensitive = true`.

### Sugestoes

#### Validar duracoes de retry com o mesmo rigor da retencao

- Severidade: Sugestao
- Arquivo e linha aproximada:
  `infra/terraform/modules/pubsub-ledger-events/variables.tf:187`
- Descricao: `min_retry_backoff` e `max_retry_backoff` possuem tipo e default,
  mas nao validam formato nem relacao entre minimo e maximo.
- Impacto pratico: valores invalidos seriam detectados apenas pelo provider ou
  no plan/apply.
- Recomendacao: adicionar validations para formato Google duration e garantir
  que `min_retry_backoff <= max_retry_backoff`.

#### Evitar endurecer demais provider com versao exata

- Severidade: Sugestao
- Arquivo e linha aproximada:
  `infra/terraform/environments/dev/main.tf:7` e
  `infra/terraform/modules/pubsub-ledger-events/main.tf:7`
- Descricao: as constraints `>= 7.0.0, < 8.0.0` sao adequadas. O lockfile do
  root module fixa a resolucao atual em `7.34.0`.
- Impacto pratico: bom equilibrio entre reprodutibilidade e atualizacoes de
  patch/minor. O risco esta mais no processo de atualizar lockfiles do que na
  constraint atual.
- Recomendacao: manter esse padrao e atualizar lockfiles de root modules via PR
  explicito.

## Validacao tecnica

Comandos executados:

| Comando | Resultado |
| --- | --- |
| `terraform version` | Passou. Terraform `v1.15.5` em `windows_amd64`. |
| `terraform fmt -check -recursive ./infra/terraform` | Passou sem arquivos a formatar. |
| `terraform "-chdir=./infra/terraform/environments/dev" init -backend=false -input=false` | Passou. Providers `hashicorp/google` e `hashicorp/google-beta` reutilizados em `7.34.0`. |
| `terraform "-chdir=./infra/terraform/environments/dev" validate` | Passou. Configuracao valida. |
| `tflint --recursive` | Passou sem achados. |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/quality/terraform/validate.ps1` | Passou. Executou `fmt`, `init -backend=false`, `validate` para root e modulo, e `tflint --recursive`. |
| `checkov --version` | Nao executado: ferramenta nao instalada no ambiente. |
| `tfsec --version` | Nao executado: ferramenta nao instalada no ambiente. |
| `terrascan version` | Nao executado: ferramenta nao instalada no ambiente. |

Comandos propositalmente nao executados:

- `terraform plan`: nao solicitado explicitamente e pode consultar recursos reais
  em GCP.
- `terraform apply`: proibido pelo prompt e pelas regras do repositorio.
- `terraform destroy`: proibido pelo prompt e pelas regras do repositorio.

## Priorizacao

### Primeiro PR

- Adicionar TFLint ao workflow `.github/workflows/terraform-validation.yml`.
- Adicionar scanner IaC informativo, preferencialmente Checkov ou tfsec,
  com baseline revisado.
- Documentar no proprio workflow que `-lock=false` e uma excecao temporaria por
  ausencia de backend remoto.

### Segundo PR

- Decidir e documentar estrategia de backend remoto para ambientes
  compartilhados.
- Criar ADR ou atualizar documentacao operacional se o state remoto passar a ser
  adotado.
- Remover `-lock=false` quando houver backend com locking.

### Terceiro PR

- Padronizar labels minimos para recursos GCP: owner, cost center ou equivalente,
  criticidade e ambiente.
- Adicionar validations para `min_retry_backoff` e `max_retry_backoff`.

### Itens que podem ficar para depois

- Criar novos modulos apenas quando houver mais recursos alem de Pub/Sub.
- Automatizar publicacao de plan como artefato ou comentario de PR, desde que sem
  secrets e sem plano binario sensivel.
- Avaliar ambiente `staging` ou `prod` somente depois da decisao de backend,
  IAM e naming por ambiente.

## Recomendacoes finais

- Mantenha `infra/terraform/environments/dev/.terraform.lock.hcl` versionado
  como fonte de verdade do root module.
- Nao trate lockfiles locais de modulos como fonte de verdade; modulos devem
  declarar constraints e os root modules devem resolver providers.
- Preserve `terraform.tfvars`, state, planos e credenciais fora do Git, como a
  `.gitignore` ja define.
- Evolua o pipeline com lint e security scan antes de adicionar Cloud Run,
  Cloud SQL, Secret Manager ou IAM mais amplo.
- Ao adotar backend remoto, separe state por ambiente e revise locking, IAM do
  bucket, retencao, auditoria e processo de migracao do state.
- Para execucao segura, continue usando `terraform plan` apenas com revisao
  humana e `terraform apply` manual depois de autorizacao explicita.

## Escopo analisado

Foram analisados:

- Arquivos `*.tf`, `*.tfvars`, `*.tfvars.example`, `*.hcl` e lockfiles em
  `infra/terraform`.
- Workflow `.github/workflows/terraform-validation.yml`.
- Scripts `scripts/quality/terraform/validate.ps1` e `scripts/quality/terraform/validate.sh`.
- Documentacao relacionada em `infra/terraform/**/README.md`,
  `docs/development/terraform-gcp-local-setup.md`,
  `docs/development/pubsub-infra-app-contract.md`,
  `docs/development/pubsub-gcp-dev-apply-checklist.md`,
  `docs/development/pull-request-validation.md` e `docs/operations/pubsub.md`.

Nada foi corrigido automaticamente na infraestrutura. A unica alteracao aplicada
foi a criacao deste relatorio. Os demais pontos ficaram como recomendacoes
priorizadas para PRs futuros.
