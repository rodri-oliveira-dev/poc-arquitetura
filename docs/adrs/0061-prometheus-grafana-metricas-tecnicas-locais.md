# ADR-0061: Prometheus e Grafana para metricas tecnicas locais

## Status
Aceito

## Data
2026-05-19

## Contexto

A stack local ja usa OpenTelemetry Collector como ponto central de entrada OTLP e Jaeger como backend de traces. As APIs `Auth.Api`, `LedgerService.Api` e `BalanceService.Api` exportam traces e metricas via OTLP para o Collector quando `Observability:OpenTelemetry:Enabled=true`.

Na etapa anterior, o Collector recebia metricas OTLP, mas as descartava com exporter `nop`. A evolucao atual precisa permitir visualizacao local de metricas tecnicas automaticas de ASP.NET Core, `HttpClient` e runtime .NET sem acoplar codigo das aplicacoes a Prometheus ou Grafana.

## Decisao

Adicionar Prometheus e Grafana ao `compose.yaml` local.

O OpenTelemetry Collector continua sendo o ponto central de entrada da telemetria das aplicacoes. O pipeline de traces permanece exportando para o Jaeger. O pipeline de metricas passa a usar o exporter `prometheus`, expondo metricas em `otel-collector:9464` apenas na rede interna do compose.

O Prometheus faz scrape somente do Collector, usando o job `otel-collector`. O Grafana e provisionado com datasource Prometheus apontando para `http://prometheus:9090`.

A imagem do Collector passa para `otel/opentelemetry-collector-contrib` na mesma versao ja usada pela stack local para disponibilizar o exporter Prometheus.

## Consequencias

- As APIs continuam exportando OTLP apenas para o Collector.
- Jaeger continua recebendo traces pelo pipeline existente.
- Prometheus nao coleta diretamente de `Auth.Api`, `LedgerService.Api` ou `BalanceService.Api`.
- Grafana usa Prometheus como datasource local provisionado.
- A porta `9464` do Collector fica exposta apenas internamente para o Prometheus.
- Logs, Loki, Alertmanager, alertas, remote write e dashboards complexos continuam fora do escopo.

## Beneficios

- Permite validar metricas tecnicas automaticas sem alterar codigo C#.
- Mantem o Collector como camada de desacoplamento entre aplicacoes e backends de observabilidade.
- Preserva a possibilidade de trocar ou adicionar backends no futuro por configuracao de infraestrutura.
- Mantem a stack simples e adequada para POC local.

## Trade-offs / custos

- A stack local passa a subir dois containers adicionais.
- Grafana local usa credenciais simples de POC (`admin`/`admin`), sem pretensao de ambiente compartilhado ou produtivo.
- As metricas ficam em memoria/armazenamento efemero do container Prometheus, pois persistencia de dados e retencao operacional nao fazem parte desta etapa.
- A disponibilidade do Grafana depende do Prometheus, e o Prometheus depende do endpoint de metricas do Collector.

## Alternativas consideradas

1. **Prometheus fazer scrape direto das APIs**
   - Rejeitado para evitar acoplamento das aplicacoes a Prometheus e preservar o Collector como entrada central.

2. **Usar remote write no Collector**
   - Rejeitado por adicionar complexidade e backend remoto fora do objetivo local.

3. **Manter exporter `nop` ate uma stack produtiva ser definida**
   - Rejeitado porque impede validar metricas tecnicas automaticas na POC local.

4. **Criar dashboards complexos agora**
   - Rejeitado porque a etapa atual e apenas habilitar coleta e datasource para validacao tecnica.
