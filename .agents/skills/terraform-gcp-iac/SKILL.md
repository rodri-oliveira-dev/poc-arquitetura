---
name: terraform-gcp-iac
description: Use esta skill para criar, revisar ou evoluir infraestrutura GCP com Terraform neste repositorio. Cobre provider Google, modulos, ambientes, state, validacao, IAM, Cloud Run, Cloud SQL, Artifact Registry, Secret Manager e guardrails para execucao segura.
---

# Objetivo

Orientar mudancas de infraestrutura como codigo em Terraform para uma futura execucao da POC em Google Cloud, mantendo seguranca, reprodutibilidade e coerencia com a arquitetura .NET existente.

# Quando usar

- Criar ou revisar arquivos `*.tf`, `*.tfvars.example`, modulos Terraform ou estrutura `infra/terraform/`.
- Planejar recursos GCP para APIs, workers, bancos PostgreSQL, imagens de container, secrets, contas de servico, rede, observabilidade ou IAM.
- Avaliar backend remoto de state, organizacao por ambientes, convencoes de nomes e variaveis.
- Revisar diagnosticos de formatacao, inicializacao, validacao ou plano Terraform.
- Criar documentacao ou ADR sobre estrategia de infraestrutura GCP.

# Quando nao usar

- Alteracoes funcionais nos servicos .NET sem impacto em infraestrutura.
- Ajustes locais de Docker Compose sem relacao com Terraform ou GCP.
- Uso exploratorio de `gcloud` sem editar IaC. Nesse caso use `gcp-cli-auth-governance`.
- Deploy real ou alteracoes remotas sem pedido explicito e criterio de seguranca.

# Passos

1. Leia `AGENTS.md`, `README.md`, `docs/architecture/`, `docs/adrs/` e a documentacao operacional relacionada ao recurso afetado.
2. Verifique se tambem se aplica alguma skill GCP especifica, como `gcp-cli-auth-governance`, `gcp-cloud-run-deployment` ou `gcp-cloud-sql-postgres`.
3. Identifique o alvo da mudanca: modulo, ambiente, provider, backend, IAM, rede, runtime, banco, secrets ou observabilidade.
4. Prefira estrutura previsivel em `infra/terraform/`, separando `modules/` de `environments/`, mas crie apenas arquivos com conteudo real.
5. Declare versoes de Terraform e providers com constraints explicitas e justificaveis.
6. Use variaveis para projeto, regiao, nomes de recursos, labels, imagens, limites e configuracoes por ambiente.
7. Nao versione state, planos binarios, credenciais, arquivos `.tfvars` com valores reais ou chaves de service account.
8. Modele IAM com menor privilegio e prefira service accounts dedicadas por workload.
9. Para APIs HTTP, avalie Cloud Run services. Para workers, avalie Cloud Run worker pools, jobs ou GKE apenas com decisao explicita.
10. Para PostgreSQL, avalie Cloud SQL com conectividade segura, backups, HA, migrations EF Core e segredos fora do repositorio.
11. Quando a decisao mudar arquitetura, ambiente, IAM, rede, persistencia, observabilidade ou deploy, atualize documentacao e avalie ADR.
12. Antes de finalizar, revise diff, ausencia de segredos e impacto operacional.

# Validacao

Use validacoes locais e nao destrutivas quando o Terraform existir no repositorio:

```bash
terraform fmt -check -recursive
terraform init -backend=false
terraform validate
```

Qualquer execucao que altere infraestrutura real precisa de aprovacao explicita do usuario.

# Restricoes

- Nao aplicar mudancas remotas de infraestrutura sem autorizacao explicita.
- Nao habilitar APIs GCP, alterar billing, organizacao, folders, KMS, IAM amplo ou politicas de org sem aprovacao explicita.
- Nao introduzir segredos, senhas, tokens, chaves JSON, connection strings reais ou valores sensiveis.
- Nao importar cegamente exemplos externos. Adapte ao contexto deste repositorio.
