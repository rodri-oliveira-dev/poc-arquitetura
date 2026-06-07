# Roadmap arquitetural consolidado

Este roadmap consolida as proximas frentes de evolucao do projeto por area de maturidade. Ele foi criado a partir do estado versionado do repositorio e deve ser lido junto com [maturidade tecnica](maturity.md), [analise arquitetural e decisoes recomendadas](architecture/decisions.md), [indice de documentacao](README.md) e [ADRs](adrs/README.md).

## Como usar este roadmap

Este documento orienta estudos incrementais e revisoes tecnicas. Ele nao representa compromisso de producao, SLA, plano comercial, certificacao de seguranca ou garantia de prontidao operacional fora do laboratorio local.

Use o roadmap para escolher proximas fatias pequenas, manter coerencia entre codigo, testes, documentacao e ADRs, e evitar reabrir temas ja atendidos. Quando uma frente virar mudanca concreta, consulte a documentacao especializada indicada em cada area.

## Criterio para mover item para feito

Um item deve sair de "Proximos passos" ou "Em andamento ou parcialmente atendido" para "Feito" quando houver evidencia versionada proporcional ao impacto:

- codigo ou documentacao versionada;
- testes, script ou workflow quando aplicavel;
- ADR quando houver decisao arquitetural relevante;
- indices de documentacao atualizados, especialmente [README.md](../README.md), [docs/README.md](README.md) e o indice de [ADRs](adrs/README.md), quando aplicavel.

## Arquitetura

### Feito

- Clean Architecture e DDD pragmaticos por microservico, com separacao entre `Api`, `Application`, `Domain`, `Infrastructure` e processos Worker.
- `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker` separados operacionalmente, conforme [README](../README.md) e [boundaries](architecture/boundaries.md).
- Pub/Sub definido como provider principal, com Kafka mantido como adapter legado opcional.
- Keycloak consolidado como identidade principal local, com `Auth.Api` fora da stack principal.
- Modelo LikeC4 versionado e publicado por workflow de Pages.

### Em andamento ou parcialmente atendido

- Algumas diferencas de padrao entre Ledger e Balance continuam aceitas de forma pragmatica, como uso de MediatR no Balance e posicao historica de algumas portas.
- Readiness das APIs cobre dependencias diretas HTTP, mas pode crescer demais se novas verificacoes forem adicionadas sem extracao.
- `OutboxMessage` permanece como escolha pragmatica de dominio/integracao, com risco conhecido se a complexidade aumentar.

### Proximos passos

- Padronizar criterio para novas portas de persistencia antes de criar novos servicos ou mover contratos internos.
- Extrair checks de readiness para componentes pequenos se a composicao em `Program.cs` crescer.
- Isolar montagem de eventos e Outbox apenas se os casos de uso crescerem ou os testes ficarem ruidosos.
- Manter diagramas LikeC4 e ADRs alinhados a cada mudanca relevante.

### Fora de escopo por enquanto

- Reorganizar todas as camadas por simetria.
- Introduzir MediatR em todos os servicos.
- Avaliar .NET Aspire antes de a orquestracao local virar gargalo real.
- Tratar a arquitetura atual como baseline produtivo.

## Contratos HTTP e OpenAPI

### Feito

- Contratos HTTP documentados para Ledger e Balance.
- OpenAPI versionado em `docs/openapi/`.
- Geracao automatizada por scripts e workflow `openapi-contract-validation`.
- Lint com Redocly, deteccao de drift e diff de breaking changes contra `main`, conforme [validacao OpenAPI](development/openapi-contract-validation.md).

### Em andamento ou parcialmente atendido

- Algumas regras de lint continuam como warning para evitar bloqueios prematuros.
- Mudancas comportamentais que nao aparecem no OpenAPI ainda dependem de revisao humana.

### Proximos passos

- Endurecer gradualmente regras OpenAPI hoje mantidas como warning, quando houver baixo risco de ruido.
- Reavaliar documentacao de status codes, payloads de erro, headers e scopes quando endpoints protegidos evoluirem.
- Manter `docs/development/ledger-api.md`, `docs/development/balance-api.md` e contratos gerados sincronizados a cada mudanca HTTP.

### Fora de escopo por enquanto

- Alterar contratos HTTP apenas para reorganizacao documental.
- Criar nova versao publica de API sem necessidade de compatibilidade real.

## Contratos de eventos

### Feito

- Contratos logicos de eventos documentados em [docs/events](events/README.md).
- JSON Schemas e exemplos versionados em [contracts/events](../contracts/events/README.md).
- Workflow `event-contract-validation` com validacao de schemas e exemplos.
- Politica de versionamento entre Pub/Sub e Kafka documentada em [event-contract-versioning](development/event-contract-versioning.md).
- `LedgerEntryCreated.v2` com `currency` obrigatoria e leitura de `v1` mantida como legado.

### Em andamento ou parcialmente atendido

- A governanca de compatibilidade precisa continuar sendo aplicada quando novos consumidores, eventos ou versoes aparecerem.
- `LedgerEntryCreated.v1` ainda existe para convivencia operacional e replay legado.

### Proximos passos

- Endurecer regras de eventos se surgirem novos consumidores ou maior risco de quebra entre contextos.
- Avaliar schema registry apenas se o projeto sair do laboratorio local para uma operacao mais ampla.
- Remover leitura de versoes legadas somente com evidencia de ausencia de backlog, producer ativo e replay esperado.

### Fora de escopo por enquanto

- Criar shared library de contratos antes de decidir governanca de distribuicao.
- Fazer Pub/Sub e Kafka divergirem no payload logico.
- Remover suporte a `v1` sem etapa dedicada.

## Seguranca

### Feito

- Keycloak local como IdP principal, JWT RS256, JWKS e validacao offline pelas APIs.
- Autorizacao por scopes e merchant documentada.
- CodeQL, Dependency Review e Trivy versionados em workflows ou hooks.
- Trivy cobre Dockerfiles, Terraform, misconfigurations, secrets e filesystem, conforme [trivy-security-scan](development/trivy-security-scan.md).
- OWASP ZAP local documentado e executavel por scripts.

### Em andamento ou parcialmente atendido

- DAST/OWASP ZAP ainda nao possui workflow automatizado nem gate.
- A maturidade de seguranca estatica depende de execucao recente dos scanners.
- Baseline produtivo GCP/seguranca ainda nao foi consolidado.

### Proximos passos

- Definir baseline produtivo para secrets, TLS interno, identidade de workload, Pub/Sub real, bancos, scans de imagem, WAF e rate limits por identidade.
- Criar workflow OWASP ZAP/DAST automatizado somente quando houver ambiente e criterio de gate adequados.
- Registrar resultados de DAST ou pentest quando a avaliacao dinamica for necessaria.

### Fora de escopo por enquanto

- Criar baseline produtivo nesta etapa.
- Automatizar OWASP ZAP sem decisao de ambiente alvo, credenciais, risco de falso positivo e politica de bloqueio.
- Recolocar `Auth.Api` como emissor principal.

## Observabilidade

### Feito

- Observabilidade local documentada com logs, traces, metricas, health, readiness, dashboards e alertas.
- OpenTelemetry opcional, correlacao por `X-Correlation-Id`, propagacao W3C pela Outbox e adapters quando aplicavel.
- Stack local com Collector, Jaeger, Prometheus, Loki, Alloy, Alertmanager e Grafana via overlay de observabilidade.
- Dashboards e alertas tecnicos versionados.

### Em andamento ou parcialmente atendido

- Fluxo Pub/Sub principal ainda nao possui o mesmo detalhamento de span de consumo documentado para Kafka legado.
- Metricas customizadas existem, mas podem evoluir com novos sinais de Pub/Sub, DLQ, replay e rebuild.

### Proximos passos

- Completar instrumentacao operacional do adapter Pub/Sub onde houver valor diagnostico claro.
- Criar metricas de baixa cardinalidade para DLQ, replay/redrive, rebuild e backlog quando os fluxos forem operacionalizados.
- Manter cardinalidade controlada em labels, preservando ids no conteudo de logs ou atributos apropriados.

### Fora de escopo por enquanto

- Provisionar stack produtiva de observabilidade.
- Transformar `CorrelationId` em substituto de trace distribuido.
- Criar framework interno de observabilidade.

## Operacao assincrona

### Feito

- Outbox transacional no Ledger e publicacao assincrona por worker.
- Pub/Sub principal com emulator local, DLQ de aplicacao e Kafka legado opcional.
- Runbooks de DLQ, retry, replay, redrive, descarte e rebuild documentados.
- Casos de uso internos para replay manual, replay filtrado, rebuild parcial e relatorio de divergencia.
- Requeue operacional de Outbox exposto no Ledger para mensagens em dead letter.

### Em andamento ou parcialmente atendido

- Nao ha endpoint administrativo publico para replay, replay por filtro, rebuild parcial ou relatorio de divergencia.
- Nao ha redrive versionado implementado para DLQ de aplicacao Pub/Sub ou Kafka.
- O relatorio de divergencia e rebuild parcial nao possuem auditoria persistente dedicada.
- A fonte atual para replay por filtro e rebuild usa `ledger.outbox_messages`.

### Proximos passos

- Definir superficie operacional controlada para executar replay, replay por filtro, rebuild e relatorio de divergencia.
- Persistir auditoria de descarte, redrive, replay e rebuild quando a operacao exigir rastreabilidade maior.
- Implementar redrive versionado de DLQ com validacao de schema, dry-run, limites e auditoria persistente.
- Avaliar adapters operacionais para ler DLQ Pub/Sub e Kafka como fontes de replay preservando metadados.

### Fora de escopo por enquanto

- Criar endpoint publico irrestrito para operacao de replay ou rebuild.
- Usar DLQ como fonte primaria de reconstrucao de saldo.
- Fazer rebuild destrutivo, troca de tabela ou correcao financeira automatica sem decisao dedicada.

## Cloud, GCP e Terraform

### Feito

- Terraform versionado para ambiente dev e modulos de Pub/Sub e Cloud SQL.
- Backend remoto GCS para Terraform dev registrado em ADR.
- Validacoes locais e de CI para Terraform, TFLint e Trivy sem executar `plan`, `apply` ou `destroy`.
- Documentacao de setup GCP, Cloud SQL com Auth Proxy, Pub/Sub real e contrato entre outputs Terraform e configuracao da aplicacao.

### Em andamento ou parcialmente atendido

- O ambiente GCP continua orientado a validacao manual e estudos controlados.
- O checklist de primeiro apply Pub/Sub e manual e voltado a projeto descartavel.
- Ainda nao existe baseline produtivo para seguranca, rede, identidade, secrets, imagens, observabilidade e operacao.

### Proximos passos

- Consolidar baseline produtivo GCP/seguranca antes de tratar a infraestrutura como referencia operacional.
- Definir estrategia completa para Cloud Run, Cloud SQL, Pub/Sub, Secret Manager, Artifact Registry, IAM minimo, budgets e logs.
- Expandir validacoes somente com guardrails claros para nao aplicar ou destruir recursos por acidente.

### Fora de escopo por enquanto

- Executar `terraform apply` automatizado.
- Criar ambiente produtivo ou compartilhado persistente nesta etapa.
- Versionar segredos, chaves ou credenciais locais.

## Testes e qualidade

### Feito

- Build, testes e cobertura em workflow `dotnet`.
- Gate minimo de 85% de cobertura total de linhas e dos assemblies Worker.
- Testes unitarios e de integracao documentados.
- Mutation testing com Stryker.NET documentado e workflow dedicado.
- SonarQube local documentado.
- Pull request validation, artifact policy, CodeQL, Dependency Review e validacoes de contratos versionadas.

### Em andamento ou parcialmente atendido

- Resultado recente de build, cobertura, SAST, mutation testing e scanners depende da ultima execucao dos workflows.
- Mutation testing e SonarQube apoiam estudos de qualidade, mas nao substituem revisao de design e testes direcionados.

### Proximos passos

- Usar analise de cobertura e CRAP score para priorizar testes em areas de maior risco.
- Evoluir testes quando novas regras de negocio, contratos, DLQ, replay, rebuild ou autorizacao forem alterados.
- Registrar resultados relevantes em relatorios quando uma validacao ampla for feita.

### Fora de escopo por enquanto

- Alterar testes apenas para aumentar cobertura numerica.
- Exigir mutation score global sem baseline e criterio de evolucao incremental.

## Performance e k6

### Feito

- Testes k6 versionados para smoke, leitura do Balance a 50 rps e escrita concorrente moderada no Ledger.
- Runners exportam summaries JSON em `artifacts/k6`.
- Criterios atuais cobrem checks falhos, `http_req_failed` e `dropped_iterations`.
- Workflow `loadtests-smoke` existe para smoke controlado.

### Em andamento ou parcialmente atendido

- Ainda nao existem thresholds formais de latencia p95/p99.
- Os testes atuais validam comportamento local/controlado, nao capacidade produtiva.
- O smoke confirma fluxo ponta a ponta via Pub/Sub emulator, sem inspecionar internals de `ack`, `nack`, DLQ ou commit Kafka legado.

### Proximos passos

- Registrar uma linha de base local reprodutivel antes de definir thresholds p95/p99 por cenario.
- Separar objetivos de latencia de API, banco, mensageria, workers e runtime Docker.
- Criar cenario dedicado ao Nginx somente se a borda local virar alvo de carga.
- Evoluir cenarios de backlog, drenagem de Outbox, duplicidade, idempotencia, retry e DLQ quando a frente assincrona pedir.

### Fora de escopo por enquanto

- Usar k6 local como prova de dimensionamento produtivo.
- Definir p95/p99 arbitrario sem baseline.
- Misturar teste funcional de API com medicao de borda Nginx sem objetivo claro.

## Documentacao e governanca

### Feito

- Indice geral em [docs/README.md](README.md) e porta de entrada no [README raiz](../README.md).
- Maturidade tecnica consolidada em [docs/maturity.md](maturity.md).
- ADRs historicas mantidas em [docs/adrs](adrs/README.md).
- Documentacao de arquitetura, contratos, operacao, qualidade, Terraform, observabilidade e troubleshooting versionada.
- Instrucoes de agentes e skills locais documentam como trabalhar no repositorio.

### Em andamento ou parcialmente atendido

- Alguns relatorios sao historicos e nao devem ser lidos como estado operacional mais recente.
- O roadmap precisa acompanhar o fechamento das frentes sem duplicar conteudo de documentos especializados.

### Proximos passos

- Atualizar este roadmap quando uma frente relevante for concluida, descartada ou reclassificada.
- Criar ADR somente quando houver decisao arquitetural nova ou mudanca relevante de comportamento.
- Manter docs detalhadas em `docs/` e usar o README raiz apenas como porta de entrada.
- Atualizar indices quando documentos forem adicionados, removidos ou consolidados.

### Fora de escopo por enquanto

- Reescrever ADR historica como documentacao atual.
- Duplicar runbooks, contratos ou comandos longos dentro deste roadmap.

## Legados e remocoes futuras

### Feito

- `Auth.Api` foi depreciado como emissor legado de POC e removido da stack principal.
- Keycloak e o caminho principal local de autenticacao.
- Kafka permanece disponivel apenas como provider legado opcional.
- `LedgerEntryCreated.v1` esta documentado como legado aceito para mensagens antigas.

### Em andamento ou parcialmente atendido

- `Auth.Api` continua no repositorio com testes proprios enquanto existir.
- Kafka legado opcional ainda e mantido para estudo e compatibilidade.
- Leitura de `LedgerEntryCreated.v1` continua necessaria para convivencia operacional.

### Proximos passos

- Remover definitivamente `Auth.Api` quando nao houver mais necessidade de compatibilidade.
- Remover ou reduzir Kafka legado somente com decisao explicita, evidencias de nao uso e atualizacao de docs, testes e ADRs.
- Remover suporte a eventos legados apenas depois de avaliar backlog, producers antigos, retencao e replay esperado.

### Fora de escopo por enquanto

- Remover `Auth.Api` nesta etapa.
- Remover Kafka legado sem uma frente dedicada.
- Apagar contratos legados ainda aceitos pelo consumidor.
