# ADR-0062: Alertas tecnicos locais com Prometheus e Alertmanager

## Status
Aceito

## Data
2026-05-19

## Contexto

A stack local ja possui OpenTelemetry Collector recebendo telemetria OTLP das APIs, Jaeger para traces, Prometheus coletando metricas tecnicas pelo Collector e Grafana com dashboards minimos.

A POC precisa passar a sinalizar falhas tecnicas simples durante validacoes locais, sem definir SLOs formais, sem criar metricas customizadas novas e sem acoplar as aplicacoes a Prometheus, Grafana ou Alertmanager.

## Decisao

Adicionar regras de alerta Prometheus versionadas em `observability/prometheus/rules/technical-alerts.yml`.

Adicionar Alertmanager local ao `compose.yaml`, com configuracao minima em `observability/alertmanager/alertmanager.yml`, receiver local sem envio externo e UI exposta em `http://localhost:9093`.

Configurar o Prometheus para:

- carregar arquivos de regras em `/etc/prometheus/rules/*.yml`;
- enviar alertas para `alertmanager:9093`;
- coletar `otel-collector:9464`, `alertmanager:9093` e o proprio Prometheus em `localhost:9090`.

As regras iniciais cobrem apenas sinais tecnicos basicos:

- disponibilidade do Collector;
- disponibilidade do Alertmanager;
- falha de reload de configuracao do Prometheus;
- taxa de respostas HTTP 5xx;
- latencia HTTP p95 elevada;
- falhas observadas no endpoint `/ready`;
- taxa elevada de excecoes .NET.

## Consequencias

- A stack local passa a ter um container adicional: `alertmanager`.
- Prometheus deixa de coletar somente o Collector e passa a coletar tambem Prometheus e Alertmanager para permitir alertas tecnicos da propria stack de alerting.
- Nao ha integracao externa de notificacao.
- Alertas de negocio, Outbox, Kafka, DLQ e SLOs permanecem fora do escopo desta decisao.
- As regras dependem dos nomes e labels reais ja usados pelos dashboards Prometheus/Grafana locais.

## Beneficios

- Permite visualizar alertas tecnicos na UI do Prometheus e do Alertmanager.
- Mantem as APIs desacopladas da stack de alerting.
- Evita labels de alta cardinalidade em regras de alerta.
- Mantem a evolucao local simples e adequada para uma POC.

## Trade-offs / custos

- Ha mais um container e uma porta local exposta (`9093`).
- Alertas HTTP dependem das metricas automaticas exportadas pelas APIs via OpenTelemetry.
- O alerta de readiness observa falhas quando o endpoint `/ready` e chamado; ele nao substitui uma sonda ativa dedicada.
- Sem integracao externa, a validacao e visual na UI local.

## Alternativas consideradas

1. **Usar Grafana Alerting**
   - Rejeitado para evitar duplicar regras e porque a stack ja usa Prometheus como fonte das metricas tecnicas.

2. **Adicionar notificacoes externas**
   - Rejeitado porque e desnecessario e inseguro para a POC local.

3. **Criar SLOs e alertas de negocio**
   - Rejeitado por depender de metricas, objetivos e runbooks que ainda nao fazem parte do escopo.

4. **Criar metricas customizadas novas para alertas**
   - Rejeitado porque a etapa atual deve usar apenas sinais tecnicos ja disponiveis.
