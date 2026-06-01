---
name: gcp-cloud-run-deployment
description: Use esta skill para desenhar, revisar ou documentar deploy de APIs, workers e jobs em Cloud Run para esta POC .NET. Cobre container, porta, variaveis, secrets, service account, escalabilidade, health, logs, Cloud SQL e limites operacionais.
---

# Objetivo

Orientar a preparacao de APIs e workers .NET deste repositorio para execucao em Cloud Run, sem executar deploy real automaticamente.

# Quando usar

- Planejar ou revisar deploy de `LedgerService.Api`, `BalanceService.Api`, workers ou jobs em Cloud Run.
- Ajustar Dockerfile, configuracao de porta, health/readiness, variaveis de ambiente, secrets ou service account para Cloud Run.
- Avaliar se um componente deve ser Cloud Run service, job, worker pool ou GKE.
- Documentar estrategia de deploy, escalabilidade, logs, metricas, custos ou limites.

# Quando nao usar

- Rodar stack local Docker Compose sem relacao com Cloud Run.
- Criar Cloud SQL, IAM ou rede como foco principal. Combine com as skills GCP correspondentes.
- Executar deploy real sem pedido explicito.

# Mapeamento inicial da POC

- APIs HTTP tendem a mapear para Cloud Run services.
- Workers Kafka exigem avaliacao cuidadosa: worker pool, job, GKE ou alternativa gerenciada dependem do modelo de mensageria escolhido.
- PostgreSQL local deve ser substituido por Cloud SQL ou por outro servico explicitamente decidido.
- Imagens devem ficar preferencialmente em Artifact Registry.
- Segredos devem vir de Secret Manager ou mecanismo equivalente, nunca de arquivo versionado.

# Passos

1. Leia `README.md`, `docs/architecture/`, `docs/observability.md`, Dockerfiles, Compose e ADRs relacionadas.
2. Classifique cada workload como service, worker pool, job ou fora de Cloud Run.
3. Confirme compatibilidade do container com porta dinamica, startup, shutdown e health checks.
4. Preserve health/readiness e logs estruturados do projeto.
5. Modele variaveis por ambiente sem versionar secrets.
6. Defina service account dedicada por workload com menor privilegio.
7. Avalie conectividade com Cloud SQL, Kafka/Pub/Sub, Keycloak/OIDC e observabilidade.
8. Defina limites de CPU, memoria, concorrencia, timeout e escala conforme o perfil do workload.
9. Para workers, valide shutdown gracioso, idempotencia e reprocessamento.
10. Prefira registrar a infraestrutura em Terraform quando a decisao for persistente.
11. Atualize documentacao e avalie ADR quando houver mudanca de arquitetura de deploy.

# Validacao

- Build de imagem ou build .NET quando a mudanca afetar containerizacao.
- Revisao de variaveis obrigatorias e secrets esperados.
- Confirmacao de health/readiness e porta.
- Revisao de logs, correlacao e OpenTelemetry quando aplicavel.

# Restricoes

- Nao executar deploy, alterar trafego, mudar exposicao de servico ou mudar IAM sem autorizacao explicita.
- Nao colocar connection strings reais, senhas, tokens ou chaves em arquivos versionados.
- Nao assumir que workers Kafka funcionam bem em Cloud Run service tradicional sem analisar ciclo de vida, shutdown e conexoes persistentes.
