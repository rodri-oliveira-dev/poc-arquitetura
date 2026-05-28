# ADR-0058: Propagacao W3C via Outbox e Kafka

## Status
Aceito

## Data
2026-05-18

## Contexto
A auditoria de observabilidade Kafka confirmou que o `LedgerService` propagava `correlation_id` de ponta a ponta, mas o trace distribuido HTTP -> Outbox -> Kafka -> Balance nao era continuo. O motivo era que `traceparent`, `tracestate` e `baggage` eram publicados pelo producer apenas a partir da `Activity` criada no polling da Outbox, sem persistir o contexto W3C do request HTTP original.

Tambem foi identificado que o `BalanceService` tratava `baggage` recebido apenas como tag, e que a manipulacao de headers W3C estava pequena, porem espalhada entre producer, consumer e DLQ.

## Decisao
Persistir metadados W3C opcionais na tabela `outbox_messages`:

- `traceparent`;
- `tracestate`;
- `baggage`.

O caso de uso grava esses campos quando existe `Activity.Current`. O `OutboxPublisherService` restaura esse contexto ao criar o span `outbox.publish`, e o adapter Kafka (`KafkaOutboxMessagePublisher`) publica os headers W3C no Kafka a partir do contexto persistido. O adapter de consumo mapeia os headers para `ReceivedMessage`, e o processor neutro do `BalanceService` usa `traceparent`/`tracestate` como parent do span `message.process` e reidrata `baggage` como baggage real da `Activity` quando possivel.

A logica de leitura, copia e propagacao W3C fica centralizada em helpers pequenos dentro da camada `Infrastructure` de cada servico, sem criar pacote compartilhado ou framework interno.

## Consequencias

### Beneficios
- Permite continuidade real do trace HTTP -> Outbox -> Kafka -> Balance quando OpenTelemetry esta habilitado e ha `Activity` ativa.
- Mantem `CorrelationId` independente de `TraceId`.
- Preserva payloads, topicos, contratos de negocio e politica de DLQ.
- Reduz duplicacao local na manipulacao de headers W3C.
- Mantem mensagens antigas compativeis, pois os novos campos sao opcionais.

### Trade-offs / custos
- Adiciona tres colunas opcionais na Outbox.
- O baggage e persistido como string W3C simples; a Outbox nao passa a ser storage generico de tracing.
- Sem OpenTelemetry/listener no request de origem, nao ha contexto W3C novo a persistir.

## Alternativas consideradas

1. **Manter o contexto apenas em headers Kafka**
   Pros: nenhuma migration.
   Contras: nao corrige a quebra HTTP -> Outbox, pois o polling ocorre depois do request.

2. **Persistir um envelope JSON generico de observabilidade**
   Pros: extensivel.
   Contras: modelagem excessiva para a POC e risco de transformar a Outbox em storage generico de tracing.

3. **Criar biblioteca compartilhada de observabilidade**
   Pros: centralizacao entre servicos.
   Contras: aumenta acoplamento e complexidade para uma logica ainda pequena.
