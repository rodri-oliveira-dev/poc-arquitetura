# ADR-0079: Terraform state local e criterios para backend remoto

## Status
Aceito

## Data
2026-06-03

## Nota de acompanhamento
A ADR-0080 adotou backend remoto GCS para o root module dev. Esta ADR permanece
como registro dos riscos do state local, dos gatilhos e da estrategia que
orientou a migracao.

## Contexto
O Terraform versionado esta concentrado no root module
`infra/terraform/environments/dev` e no modulo reutilizavel
`infra/terraform/modules/pubsub-ledger-events`. O ambiente dev provisiona
recursos reais em GCP para o fluxo Pub/Sub: habilitacao de API, service identity
gerenciada, topics, subscriptions, service accounts e bindings IAM.

Neste momento nao ha backend remoto configurado. O ambiente dev usa o state
local padrao do Terraform. Isso e aceitavel apenas como excecao de POC, para
execucao individual ou ambiente controlado, porque a validacao principal do
repositorio ainda e local e nao destrutiva (`fmt`, `init -backend=false`,
`validate`, TFLint e Trivy). A partir do momento em que mais pessoas ou
automacoes passarem a executar Terraform contra o mesmo ambiente real, o state
local deixa de ser uma simplificacao segura.

## Decisao
Manter o state local no ambiente dev apenas enquanto a POC permanecer
controlada, individual e sem ambiente persistente compartilhado.

Nao configurar backend remoto neste momento. A configuracao de backend remoto,
a criacao de bucket GCS, a migracao de state e qualquer mudanca em recursos
reais de infraestrutura ficam fora desta decisao atual e exigem PR proprio.

Quando um dos gatilhos abaixo ocorrer, o projeto deve adotar backend remoto
antes de promover o uso compartilhado do Terraform:

- mais de uma pessoa executando Terraform contra o mesmo projeto ou ambiente;
- ambiente dev persistente ou compartilhado;
- criacao de ambiente `staging` ou `production`;
- execucao de `terraform plan` real no CI;
- provisionamento de recursos mais criticos que Pub/Sub dev descartavel;
- necessidade de historico, auditoria ou locking operacional.

## Riscos do state local
O state local aumenta o risco operacional quando o Terraform administra recursos
reais:

- divergencia de state entre maquinas;
- perda de historico local por limpeza, troca de maquina ou sobrescrita;
- concorrencia entre operadores sem protecao compartilhada;
- `plan` baseado em state incompleto, antigo ou diferente da realidade;
- dificuldade de auditoria sobre quem planejou, migrou ou alterou o ambiente.

Esses riscos existem mesmo quando `terraform plan` nao altera recursos, porque
um plano calculado sobre state incorreto pode induzir um `apply` posterior
errado.

## Estrategia futura
Quando os gatilhos forem atendidos, a estrategia preferencial e usar backend
remoto em Google Cloud Storage:

- bucket GCS dedicado para Terraform state, criado e revisado em PR proprio;
- state separado por ambiente;
- prefixo isolado por projeto, dominio e ambiente, por exemplo
  `poc-arquitetura/pubsub/dev`;
- IAM minimo no bucket de state, separado das permissoes dos workloads;
- versionamento habilitado no bucket;
- politica de retencao compativel com rollback e auditoria operacional;
- migracao de state controlada, com backup local, revisao do backend,
  `terraform init -migrate-state` e validacao posterior;
- remocao de qualquer padrao local que desabilite locking quando o backend
  remoto oferecer protecao de concorrencia.

O bucket de state nao deve ser criado pelo mesmo state que ele armazena, salvo
decisao explicita de bootstrap com procedimento documentado.

## Uso de `-lock=false`
`-lock=false` so e aceitavel no contexto atual sem backend remoto e sem state
compartilhado, como excecao para validacoes ou plans controlados que nao contam
com locking remoto.

Esse padrao nao deve ser copiado para ambientes compartilhados, persistentes,
`staging`, `production` ou CI com `terraform plan` real. Quando houver backend
remoto com suporte a locking, a flag deve ser removida e o fluxo deve validar o
locking do backend em vez de contorna-lo.

O workflow versionado `terraform-validation` nao executa `terraform plan` hoje:
ele roda Trivy, `terraform fmt -check -recursive`, `terraform init
-backend=false`, `terraform validate` e TFLint sem credenciais GCP. Se o CI
passar a executar `terraform plan` real contra GCP, isso sera um gatilho para
backend remoto e para remover qualquer uso de `-lock=false`.

## Consequencias

### Beneficios
- Mantem a POC simples e manual enquanto o uso e individual/controlado.
- Evita implementar backend remoto sem uma decisao explicita de ambiente.
- Define criterios objetivos para parar de usar state local antes de
  colaboracao ou automacao real.
- Registra que recursos GCP reais aumentam a responsabilidade operacional mesmo
  no ambiente dev.

### Trade-offs e riscos aceitos
- Enquanto o state local existir, `terraform apply` e `terraform destroy`
  continuam exigindo execucao manual, revisao humana e cuidado com o projeto
  GCP alvo.
- O state local nao oferece historico centralizado, auditoria ou locking
  compartilhado.
- O ambiente dev nao deve ser tratado como persistente ou compartilhado ate a
  migracao para backend remoto.

## Fora do escopo
- Criar bucket GCS.
- Configurar backend remoto.
- Migrar state.
- Executar `terraform plan` contra GCP.
- Executar `terraform apply`.
- Executar `terraform destroy`.
- Alterar recursos Terraform.
