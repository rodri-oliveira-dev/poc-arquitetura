# ADR-0063: Loki e Grafana Alloy para logs centralizados locais

## Status
Aceito

## Data
2026-05-19

## Contexto

A stack local ja possui OpenTelemetry Collector para telemetria OTLP, Jaeger para traces, Prometheus para metricas tecnicas, Alertmanager para alertas tecnicos locais e Grafana para visualizacao. As APIs `Auth.Api`, `LedgerService.Api` e `BalanceService.Api` ja escrevem logs no console com `CorrelationId`, `TraceId` e `SpanId` em logging scopes quando ha `Activity` ativa.

A POC precisa centralizar logs dos containers para consulta local por servico, container, ambiente, `CorrelationId` e `TraceId`, sem mudar regras de negocio, tracing distribuido, contratos Kafka, provider de logging das aplicacoes ou stack de metricas. Promtail nao deve ser usado como primeira opcao porque esta em manutencao ate 2026-02-28 e em fim de vida em 2026-03-02.

## Decisao

Adicionar Loki e Grafana Alloy ao `compose.yaml` local.

O Loki armazena logs localmente e expoe a API HTTP em `http://localhost:3100`. O Grafana Alloy coleta logs dos containers Docker do projeto compose `poc-arquitetura` via Docker API, aplica labels estaveis de baixa cardinalidade e envia os logs para o Loki. Como o Alloy precisa montar `/var/run/docker.sock`, ele fica isolado no profile `observability` do compose local.

O Grafana existente passa a receber tambem um datasource Loki provisionado em `observability/grafana/provisioning/datasources/datasources.yml`, preservando o datasource Prometheus existente como default.

Labels de stream permitidas nesta etapa:

- `service`, vindo de `com.docker.compose.service`;
- `container`, vindo do nome do container;
- `compose_project`, vindo de `com.docker.compose.project`;
- `environment`, fixo como `local`.

`CorrelationId`, `TraceId`, `SpanId`, `event_id`, `outbox_message_id`, `merchant_id`, `idempotency_key`, payloads, mensagens de exception e identificadores por requisicao, evento, mensagem ou usuario nao sao labels. Esses valores permanecem no conteudo do log e devem ser pesquisados por filtro textual ou parsing em LogQL.

A descoberta automatica de `service_name` e `detected_level` do Loki fica desabilitada na configuracao local para manter a lista de labels de stream controlada pela POC.

## Consequencias

- As aplicacoes continuam escrevendo logs no console pelo pipeline padrao do ASP.NET Core.
- Loki e Alloy nao ficam no caminho critico das APIs; se falharem, o fluxo funcional continua.
- Grafana passa a consultar metricas no Prometheus e logs no Loki.
- A stack local passa a ter dois containers adicionais e uma porta local adicional para Loki (`3100`).
- O Alloy exige acesso somente leitura ao socket Docker para descobrir e ler logs dos containers.
- A stack minima com `docker compose up -d --build` nao inicia o Alloy por padrao; para coleta de logs via Docker API, use `docker compose --profile observability up -d --build` ou os scripts locais que sobem a stack completa.
- Nao sao adicionados Promtail, Elasticsearch, OpenSearch, ELK, Serilog, alertas novos, metricas customizadas ou dashboards complexos.

## Beneficios

- Permite consultar logs centralizados por servico, container, projeto compose e ambiente.
- Mantem `CorrelationId` e `TraceId` pesquisaveis sem transformar identificadores de alta cardinalidade em labels.
- Usa Grafana Alloy, sucessor recomendado para coleta local de logs, evitando adotar Promtail em fim de vida.
- Preserva o desacoplamento entre aplicacoes e backends de observabilidade.
- Mantem a solucao simples, local e reprodutivel por Docker Compose.

## Trade-offs / custos

- A stack local fica um pouco mais pesada por incluir Loki e Alloy.
- A coleta depende do socket Docker montado no container Alloy, mesmo em modo somente leitura. Esse socket e superficie sensivel e nao deve ser usado em ambiente compartilhado ou produtivo sem reavaliar arquitetura, permissoes e isolamento.
- A retencao e persistencia de logs sao efemeras/simples nesta POC; operacao produtiva de Loki nao faz parte desta decisao.
- Consultas por `CorrelationId` e `TraceId` usam filtro textual, nao lookup por label, para evitar alta cardinalidade.

## Alternativas consideradas

1. **Usar Promtail**
   - Rejeitado porque Promtail esta em manutencao e fim de vida; Alloy e a opcao preferida para novas configuracoes.

2. **Enviar logs direto das aplicacoes para Loki**
   - Rejeitado para evitar mudar provider de logging, adicionar dependencias ou acoplar as APIs ao backend de logs.

3. **Adicionar Elasticsearch/OpenSearch/ELK**
   - Rejeitado por complexidade e por estar fora do objetivo local simples da POC.

4. **Usar `CorrelationId` e `TraceId` como labels**
   - Rejeitado por alta cardinalidade. Esses campos devem permanecer pesquisaveis no conteudo do log.

5. **Criar dashboards de logs nesta etapa**
   - Rejeitado para manter a etapa focada em coleta, armazenamento e consulta via Explore.
