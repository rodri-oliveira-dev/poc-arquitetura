# Observabilidade

Este documento define o baseline minimo de observabilidade da POC para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`.

OpenTelemetry fica desabilitado por padrao. A correlacao via `X-Correlation-Id` permanece sempre ativa nas APIs e e usada para conectar logs, respostas HTTP e mensagens Kafka.

## Baseline

- Logs: console logging do ASP.NET Core, com escopo de `CorrelationId` nos middlewares de correlacao.
- Traces: OpenTelemetry opcional para ASP.NET Core e `HttpClient`.
- Metricas: OpenTelemetry opcional para ASP.NET Core, `HttpClient` e runtime .NET.
- Exporters: console para validacao local e OTLP quando `OtlpEndpoint` estiver configurado.
- Correlacao: header HTTP `X-Correlation-Id`, campo `CorrelationId` em logs e `correlation_id` em eventos Kafka.

## Configuracao

As APIs usam a secao `Observability:OpenTelemetry`:

```json
{
  "Observability": {
    "OpenTelemetry": {
      "Enabled": false,
      "UseConsoleExporter": false,
      "OtlpEndpoint": "",
      "ServiceName": "LedgerService.Api"
    }
  }
}
```

Variaveis de ambiente equivalentes:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__UseConsoleExporter = "true"
$env:Observability__OpenTelemetry__OtlpEndpoint = "http://localhost:4317"
$env:Observability__OpenTelemetry__ServiceName = "LedgerService.Api"
```

Use `ServiceName` conforme o servico:

- `Auth.Api`
- `LedgerService.Api`
- `BalanceService.Api`

## Ambientes

### Development ou Local

Para validar sem backend de observabilidade, habilite o exporter de console:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__UseConsoleExporter = "true"
```

Para enviar traces e metricas para um collector OTLP local:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__OtlpEndpoint = "http://localhost:4317"
```

### Test

Mantenha OpenTelemetry desabilitado, salvo em testes especificos de instrumentacao. Isso evita ruido e dependencia de collector externo.

### Ambientes compartilhados ou produtivos

Habilite OpenTelemetry por configuracao de ambiente, nunca alterando o default versionado:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__OtlpEndpoint = "https://otel-collector.example:4317"
$env:Observability__OpenTelemetry__UseConsoleExporter = "false"
```

O endpoint real deve ser fornecido pela plataforma de execucao ou secret/config store. Este repositorio nao provisiona collector, dashboard, Jaeger, Tempo, Prometheus ou stack equivalente.

## Logs

Os logs usam o pipeline padrao do ASP.NET Core. `LedgerService.Api` e `BalanceService.Api` habilitam `Logging:Console:IncludeScopes=true`, o que permite incluir `CorrelationId` no console quando o provider exibe scopes.

Campos operacionais esperados:

- `CorrelationId`: valor do header `X-Correlation-Id`.
- `TraceId` e `SpanId`: adicionados ao logging scope em `LedgerService.Api` e `BalanceService.Api` quando ha `Activity` ativa.

## Traces

Quando `Observability:OpenTelemetry:Enabled=true`, as APIs registram:

- spans de entrada HTTP via instrumentacao ASP.NET Core;
- spans de saida HTTP via instrumentacao `HttpClient`.

`LedgerService` e `BalanceService` tambem criam `Activity` em trechos de Kafka/Outbox ja instrumentados no codigo. Quando ha `Activity`, headers W3C como `traceparent` e `baggage` sao propagados nas mensagens.

## Metricas

Quando OpenTelemetry esta habilitado, as APIs registram metricas de:

- ASP.NET Core;
- `HttpClient`;
- runtime .NET.

As metricas sao exportadas para console quando `UseConsoleExporter=true` e para OTLP quando `OtlpEndpoint` esta preenchido.

## Correlation id

O header padrao e `X-Correlation-Id`:

- se vier ausente ou invalido, a API gera um UUID;
- o valor efetivo e devolvido no response;
- o valor entra no logging scope como `CorrelationId`;
- eventos Kafka usam `correlation_id` quando o fluxo possui esse valor.

`CorrelationId` nao substitui trace distribuido. Ele e um identificador estavel de operacao para suporte e auditoria leve; traces e spans continuam sendo a fonte para analise temporal detalhada quando OpenTelemetry esta habilitado.

## Validacao rapida

1. Habilite OpenTelemetry com console exporter.
2. Suba a API desejada.
3. Execute uma chamada HTTP enviando ou omitindo `X-Correlation-Id`.
4. Verifique:
   - header `X-Correlation-Id` no response;
   - logs com `CorrelationId`;
   - spans e metricas no console, quando `UseConsoleExporter=true`;
   - chegada de traces e metricas no collector, quando `OtlpEndpoint` estiver configurado.

