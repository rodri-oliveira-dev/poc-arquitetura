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

# Regras de decisao

Nao introduza health/readiness, portas HTTP, endpoints auxiliares, sidecars ou novos recursos de Cloud Run apenas por checklist.

Antes de propor mudanca operacional, classifique se ela e:

1. exigida pela plataforma;
2. util, mas opcional;
3. valida apenas em producao;
4. desnecessaria para a POC;
5. potencialmente prejudicial pelo custo, complexidade ou carga operacional.

Para workers e jobs, nao introduza endpoints HTTP de health/readiness por padrao. Avalie primeiro se o ambiente de execucao exige probes HTTP ou se metricas, logs, exit code, retry policy e alertas cobrem melhor a saude operacional.

Se endpoints forem realmente necessarios, mantenha-os leves e baseados em estado interno do processo. Evite readiness que consulte banco pesado, fila, DLQ ou views de grande volume em tempo real.

# Passos

1. Leia `README.md`, `docs/architecture/`, `docs/observability.md`, Dockerfiles, Compose e ADRs relacionadas.
2. Classifique cada workload como service, worker pool, job ou fora de Cloud Run.
3. Confirme compatibilidade do container com porta dinamica, startup, shutdown e sinais de encerramento.
4. Para APIs HTTP, preserve health/readiness ja existentes quando fizerem sentido para a plataforma.
5. Para workers e jobs, avalie se health/readiness HTTP e realmente necessario ou se observabilidade por metricas e alertas e mais adequada.
6. Modele variaveis por ambiente sem versionar secrets.
7. Defina service account dedicada por workload com menor privilegio.
8. Avalie conectividade com Cloud SQL, Kafka/Pub/Sub, Keycloak/OIDC e observabilidade.
9. Defina limites de CPU, memoria, concorrencia, timeout e escala conforme o perfil do workload.
10. Atualize documentacao e ADR quando a decisao afetar estrategia de deploy, operacao, conectividade, observabilidade, seguranca ou custo.
