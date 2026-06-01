---
name: gcp-cli-auth-governance
description: Use esta skill para tarefas com Google Cloud CLI, autenticacao, ADC, service accounts, impersonation, IAM de menor privilegio e descoberta segura de recursos GCP. Nao use para client libraries ou Terraform sem IaC.
---

# Objetivo

Orientar o uso seguro de Google Cloud CLI e autenticacao GCP em tarefas de descoberta, diagnostico e preparacao operacional do repositorio.

Esta skill complementa `terraform-gcp-iac`: use esta skill para investigacao via CLI e autenticacao; use Terraform para mudancas versionadas de infraestrutura.

# Quando usar

- O pedido envolver `gcloud`, ADC, Service Account, Workload Identity Federation, impersonation ou IAM.
- For necessario descobrir projeto, regiao, recursos existentes, Artifact Registry, Cloud Run, Cloud SQL, logs ou configuracoes GCP.
- Houver troubleshooting de permissao, credenciais, token, identidade do workload ou acesso entre servicos.
- For preciso revisar comandos GCP para automacao segura.

# Quando nao usar

- Escrever codigo .NET usando client libraries Google Cloud.
- Criar ou revisar Terraform como fonte de verdade. Use tambem `terraform-gcp-iac`.
- Executar deploy, alteracao de IAM, enable de API ou mudanca remota sem autorizacao explicita.

# Passos

1. Identifique quem autentica: pessoa desenvolvedora, pipeline, agente local ou workload em producao.
2. Identifique onde o codigo roda: local, GitHub Actions, Cloud Run, GKE ou outro ambiente.
3. Identifique o recurso alvo e o menor conjunto de permissoes necessario.
4. Prefira impersonation ou Workload Identity Federation em vez de chaves JSON de service account.
5. Antes de sugerir ou executar comando, valide a sintaxe pela documentacao ou help do comando especifico.
6. Use sempre projeto e localizacao explicitos quando o comando depender de escopo.
7. Reduza saidas de descoberta com filtros, limites e formatos objetivos.
8. Nao execute comandos interativos em automacao. Use comandos revisaveis e deterministas.
9. Para troubleshooting, colete apenas dados necessarios: status, nomes, regioes, labels, logs recentes e mensagens de erro.
10. Se a investigacao indicar mudanca permanente, proponha Terraform ou documente a excecao.

# Guardrails

- Nao habilite APIs GCP sem pedido explicito.
- Nao altere billing, organizacao, folders, KMS, IAM amplo ou politicas de org sem aprovacao explicita.
- Nao crie nem versione chaves JSON de service account.
- Nao exponha tokens, secrets, connection strings ou identificadores sensiveis em logs ou documentacao.
- Nao dependa de projeto, conta ou regiao padrao quando a tarefa puder afetar recursos reais.

# Validacao

Ao finalizar, informe:

- identidade esperada para a operacao;
- projeto e regiao considerados;
- comandos ou checks usados;
- riscos de permissao ou custo;
- se a mudanca deve virar Terraform, ADR ou documentacao operacional.
