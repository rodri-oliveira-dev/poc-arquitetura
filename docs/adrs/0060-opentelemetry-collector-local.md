# ADR-0060: OpenTelemetry Collector na stack local

## Status
Aceito

## Data
2026-05-19

## Contexto

A POC ja possui OpenTelemetry opcional nas APIs, Jaeger local no `compose.yaml`, propagacao W3C entre HTTP, Outbox, Kafka e Balance, e documentacao operacional em `docs/observability.md`.

O desenho local anterior fazia as aplicacoes exportarem OTLP diretamente para o Jaeger:

```text
Aplicacoes -> Jaeger
```

Esse desenho funciona para tracing local, mas acopla a configuracao das aplicacoes ao backend de visualizacao e dificulta evolucoes futuras de roteamento, filtragem, enriquecimento ou multiplos backends.

## Decisao

Adicionar o OpenTelemetry Collector como ponto intermediario da stack local:

```text
Aplicacoes -> OpenTelemetry Collector -> Jaeger
```

O `compose.yaml` passa a incluir o servico `otel-collector`, usando imagem oficial versionada do OpenTelemetry Collector e configuracao em `observability/otel-collector-config.yaml`.

As APIs continuam configuradas por `Observability:OpenTelemetry:OtlpEndpoint`, mas no compose local passam a enviar OTLP para `http://otel-collector:4317`.

O Collector:

- recebe OTLP gRPC em `4317`;
- recebe OTLP HTTP em `4318`;
- aplica `batch` em traces;
- exporta traces para o Jaeger via `otlp_grpc` em `jaeger:4317`;
- recebe metricas OTLP e as descarta explicitamente com exporter `nop`, pois Prometheus, Grafana e dashboards nao fazem parte desta etapa.

O Jaeger continua como backend local de visualizacao de traces e UI em `http://localhost:16686`.

## Consequencias

- As aplicacoes deixam de depender diretamente do Jaeger no compose local.
- A configuracao de endpoint OTLP permanece externa ao codigo C#.
- O Jaeger continua disponivel para validacao dos traces existentes.
- A stack fica preparada para adicionar backends futuros de metricas, logs ou traces sem redesenhar as APIs.
- Metricas OTLP enviadas pelas APIs nao sao visualizadas nesta etapa; elas sao recebidas e descartadas pelo Collector ate existir decisao de backend.

## Beneficios

- Reduz acoplamento entre aplicacoes e backend de observabilidade.
- Introduz um ponto unico de coleta e roteamento de telemetria.
- Mantem baixo impacto: sem alteracao de regra de negocio, contratos Kafka, payloads, headers ou codigo C#.
- Preserva Jaeger como ferramenta local simples para diagnostico de traces.

## Trade-offs / custos

- Adiciona mais um container ao startup local.
- Falhas de configuracao do Collector podem impedir a exportacao de traces, embora nao devam quebrar o fluxo de negocio das APIs.
- O Collector precisa ser mantido alinhado aos exporters/backends que forem adicionados futuramente.

## Alternativas consideradas

1. **Manter exportacao direta para Jaeger**
   - Rejeitado porque preserva acoplamento direto das APIs ao backend local de traces.

2. **Adicionar Prometheus, Grafana ou Loki junto com o Collector**
   - Rejeitado por ampliar o escopo. Esta etapa deve introduzir apenas coleta e roteamento de traces.

3. **Alterar codigo C# para conhecer o Collector**
   - Rejeitado porque o endpoint OTLP ja e configuravel por ambiente e nao deve acoplar as aplicacoes ao componente de coleta.
