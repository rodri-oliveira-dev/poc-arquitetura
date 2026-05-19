# ADR-0059: Metricas customizadas com System.Diagnostics.Metrics

## Status
Aceito

## Data
2026-05-19

## Contexto

O baseline de observabilidade ja habilita OpenTelemetry opcional para traces e metricas automaticas de ASP.NET Core, `HttpClient` e runtime .NET. A proxima evolucao e permitir metricas customizadas tecnicas, sem introduzir Prometheus, Grafana, OpenTelemetry Collector, dashboards ou alertas nesta etapa.

Tambem e necessario evitar labels de alta cardinalidade, pois identificadores por requisicao, usuario, payload ou entidade podem aumentar custo e reduzir utilidade operacional das series temporais.

## Decisao

Adotar `System.Diagnostics.Metrics` como fundacao para metricas customizadas do projeto.

As metricas customizadas devem:

- usar `Meter` por servico, dominio tecnico ou componente operacional;
- ser registradas explicitamente no pipeline OpenTelemetry Metrics com `AddMeter(...)`;
- usar nomes em lowercase separados por ponto, no formato `<service_or_domain>.<component>.<operation>.<measure>`;
- usar unidades UCUM quando aplicavel; contadores de ocorrencias usam unidade `1`;
- descrever objetivo, evento medido e escopo da metrica no proprio instrumento;
- usar apenas labels de baixa cardinalidade, como `service`, `operation`, `event_type`, `topic`, `status` e `result`;
- evitar labels de alta cardinalidade, incluindo `correlation_id`, `trace_id`, `span_id`, `event_id`, `outbox_message_id`, `merchant_id`, identificadores de usuario, documentos, payloads e valores unicos por requisicao.

A primeira metrica customizada aceita e `ledger.outbox.publish.attempts`, um contador tecnico de tentativas de publicacao de mensagens Outbox no Kafka pelo `LedgerService.Api`.

## Consequencias

- O projeto passa a ter um ponto claro e pequeno para evoluir metricas customizadas.
- OpenTelemetry continua opcional por configuracao; quando desabilitado, os instrumentos podem ser chamados sem exporter ativo.
- A fundacao nao altera regras de negocio, contratos Kafka, payloads, headers, topicos ou stack local.
- Novas metricas devem seguir a convencao documentada antes de serem adicionadas.

## Beneficios

- Prepara a POC para metricas tecnicas futuras sem adicionar stack operacional pesada.
- Mantem baixa cardinalidade por padrao.
- Usa API nativa do .NET, testavel com `MeterListener` e desacoplada de Jaeger.
- Preserva o desenho atual de OpenTelemetry opt-in e exporters por configuracao.

## Trade-offs / custos

- A visualizacao local de metricas continua limitada ao exporter configurado, pois dashboards ficam fora do escopo.
- A existencia do `Meter` nao garante coleta se `Observability:OpenTelemetry:Enabled=false`.
- Cada nova metrica exige revisao de nome, unidade, descricao e labels antes de entrar.

## Alternativas consideradas

1. **Adicionar Prometheus e Grafana agora**
   - Rejeitado por ampliar a stack local e fugir do objetivo desta etapa.

2. **Criar framework interno de metricas**
   - Rejeitado por complexidade desnecessaria para a POC. Classes pequenas por componente sao suficientes.

3. **Usar apenas metricas automaticas do OpenTelemetry**
   - Rejeitado porque nao cria a fundacao para instrumentacao tecnica especifica de Outbox, Kafka e fluxos internos.
