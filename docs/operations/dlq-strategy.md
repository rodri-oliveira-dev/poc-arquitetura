# Estrategia operacional de DLQ

Este documento define a estrategia operacional para tratar mensagens em DLQ nos
providers de mensageria do projeto. Pub/Sub e o provider principal. Kafka
permanece como provider legado opcional. A estrategia nao cria endpoint, script,
workflow ou mecanismo novo de replay.

A DLQ existe para isolar mensagens que nao puderam ser processadas com
seguranca, preservar evidencia para investigacao e impedir retry infinito de
poison messages. Ela nao substitui idempotencia, validacao de contrato,
observabilidade nem correcao da causa raiz.

## Principios

- Separe tres operacoes: requeue da Outbox, redrive de DLQ de aplicacao e replay
  de fatos do dominio.
- Requeue da Outbox atua antes da entrega ao broker e corrige falha de
  publicacao.
- Redrive de DLQ atua depois que uma mensagem ja foi rejeitada no consumo.
- Replay de fatos do dominio deve partir da fonte de verdade do Ledger e nao da
  DLQ.
- Pub/Sub e Kafka entregam mensagens no modelo at-least-once. Duplicidade e
  esperada.
- A idempotencia do Balance depende do identificador logico do payload
  financeiro, especialmente `payload.id`, e nao do offset Kafka, message id
  Pub/Sub ou id tecnico da Outbox.
- Mensagem em DLQ nunca deve voltar automaticamente para o topic principal sem
  classificacao, validacao de contrato e registro da decisao operacional.

## Quando enviar para DLQ

Uma mensagem deve ir para DLQ de aplicacao quando o consumidor consegue
classificar a falha como nao recuperavel ou insegura para retry automatico:

- JSON invalido ou payload nao desserializavel;
- `event_type` ausente, invalido ou nao suportado pelo consumidor;
- `eventName` ou `eventVersion` impossiveis de derivar do metadado de
  transporte;
- payload que nao respeita o contrato da versao informada;
- campos obrigatorios ausentes;
- campos extras rejeitados pelo contrato do processor;
- erro de regra de negocio classificado como permanente;
- mensagem operacional enviada para consumidor errado, como evento de
  reprocessamento tratado como fato financeiro;
- falha nao recuperavel apos o processor concluir que retry nao mudara o
  resultado.

Falhas recuperaveis de infraestrutura, como indisponibilidade temporaria de
banco, timeout, broker ou credencial corrigivel, devem preferir retry do
transporte ou da Outbox antes de DLQ, conforme o ponto do fluxo.

## Quando nao reprocessar automaticamente

Nao reprocessar automaticamente quando:

- o payload e invalido;
- a versao do evento nao e suportada;
- falta `event_type`, `eventName` ou `eventVersion`;
- a causa raiz ainda nao foi corrigida;
- a mensagem representa duplicidade ja aplicada;
- a mensagem viola regra de negocio permanente;
- a DLQ foi causada por bug de contrato ainda sem decisao de compatibilidade;
- o redrive exigiria editar payload manualmente;
- o replay buscado e reconstrucao de projecao ja materializada;
- nao ha observabilidade suficiente para confirmar o resultado.

Nesses casos, a decisao deve ser descarte auditado, correcao formal do contrato,
nova publicacao por fluxo de dominio ou decisao arquitetural especifica.

## Classificacao de falhas

| Classe | Exemplos | Acao recomendada |
| --- | --- | --- |
| Erro transitorio | Timeout, conexao temporariamente indisponivel, broker instavel, lock expirado, throttling corrigivel. | Retry pelo mecanismo apropriado. Pub/Sub usa `nack` e redelivery. Kafka evita commit do offset. Outbox usa backoff e `NextRetryAt`. |
| Erro permanente | Topic incorreto, consumer sem suporte ao evento, configuracao incompatibilidade persistente, erro funcional que sempre falha. | Corrigir causa raiz. Nao reprocessar automaticamente. Avaliar descarte ou redrive controlado apos correcao. |
| Payload invalido | JSON quebrado, campo obrigatorio ausente, tipo errado, campo extra rejeitado. | DLQ de aplicacao e descarte auditado, salvo decisao formal de novo evento corretivo. |
| Versao de evento nao suportada | `LedgerEntryCreated.v3` recebido sem suporte, versao ausente ou versao divergente do payload. | DLQ de aplicacao. Nao redrivar ate existir suporte ou decisao de compatibilidade. |
| Duplicidade | Mesmo `payload.id` ja existe em `processed_events`. | Tratar como sucesso idempotente. Nao enviar para DLQ e nao tentar replay para alterar saldo. |
| Erro de regra de negocio | Merchant invalido para a operacao, valor semanticamente impossivel, evento operacional no consumidor financeiro. | Se permanente, DLQ ou descarte auditado. Se depende de dado temporario, retry controlado. |
| Falha de infraestrutura | Banco indisponivel, Pub/Sub/Kafka indisponivel, permissao IAM/ACL faltante, falha ao publicar DLQ. | Retry sem confirmar a mensagem original. Corrigir infraestrutura antes de redrive. |

## Campos minimos para investigacao

Toda investigacao de DLQ deve tentar reunir:

| Campo | Uso operacional |
| --- | --- |
| `eventId` | Identificador tecnico do evento ou mensagem, quando existir. Nao assumir que e o idempotency id do Balance. |
| `eventName` | Nome logico do evento, por exemplo `LedgerEntryCreated`. |
| `eventVersion` | Versao logica, por exemplo `v1` ou `v2`. |
| `occurredAt` | Data em que o fato ou solicitacao ocorreu no dominio. |
| `merchantId` | Escopo funcional e de autorizacao da operacao. |
| `idempotencyKey` | Chave HTTP ou operacional original, quando existir. |
| `payloadHash` | Hash do payload, se existir, para comparar tentativas sem expor payload sensivel. |
| Erro original | Motivo da DLQ, exception type, mensagem resumida e classe da falha. |
| Data da falha | Momento em que a falha foi detectada ou publicada na DLQ. |

Campos complementares importantes:

- `correlation_id` ou `CorrelationId`;
- `traceparent`, `tracestate` e `baggage`;
- `source` e provider original;
- topic, subscription, partition, offset, key ou message id quando aplicavel;
- attributes Pub/Sub ou headers Kafka originais.

## Contratos e idempotencia

O contrato logico do evento deve ser o mesmo em Pub/Sub e Kafka. O provider muda
metadados de transporte, nao a semantica do payload.

Use `event_type` para derivar:

| `event_type` | `eventName` | `eventVersion` |
| --- | --- | --- |
| `LedgerEntryCreated.v1` | `LedgerEntryCreated` | `v1` |
| `LedgerEntryCreated.v2` | `LedgerEntryCreated` | `v2` |
| `LancamentoEstornoSolicitado.v1` | `LancamentoEstornoSolicitado` | `v1` |
| `ReprocessamentoLancamentosSolicitado.v1` | `ReprocessamentoLancamentosSolicitado` | `v1` |

Regras:

- nao inferir versao apenas pelo formato do payload se `event_type` estiver
  ausente;
- validar payload contra o schema ou processor da versao informada;
- preservar `payload.id` em qualquer redrive de `LedgerEntryCreated`;
- nao trocar `payload.id` para forcar reprocessamento, pois isso bypassa
  idempotencia;
- duplicidade idempotente deve ser tratada como resultado esperado;
- replay de fatos ja aplicados nao corrige regra historica incorreta da
  projecao.

## Pub/Sub

### Componentes

| Item | Uso |
| --- | --- |
| Topic principal | Topic configurado em `PubSub:Producer:DefaultTopicId` e `TopicMap`. No local, `ledger.ledgerentry.created.local`. |
| Subscription | Subscription do `BalanceService.Worker`. No local, `balance-service-ledger-events-local`. |
| Dead letter topic de aplicacao | Topic configurado em `PubSub:Consumer:DeadLetterTopicId`. No local, `ledger.ledgerentry.created.dlq.local`. |
| Subscription de inspecao da DLQ | Subscription usada para investigar a DLQ de aplicacao. No local, `ledger-events-application-dlq-inspection-local`. |
| DLQ tecnica nativa | Recurso do Pub/Sub real provisionavel por Terraform. O emulator local nao simula essa politica. |

### Ack, nack e retry

- `ack`: usar quando a mensagem foi processada com sucesso, quando duplicidade
  foi tratada como sucesso idempotente ou quando a DLQ de aplicacao foi
  publicada com sucesso.
- `nack`: usar quando a falha e recuperavel e a mensagem original deve ser
  entregue novamente.
- Retry: depende de redelivery do Pub/Sub, ack deadline, retry policy nativa e
  comportamento do consumer. Nao transformar falha transitoria em DLQ antes de
  esgotar a politica definida.
- DLQ tecnica nativa: indica falha de entrega pelo transporte. Antes de redrive,
  validar contrato e confirmar que a causa de entrega foi corrigida.

### Attributes necessarios

Attributes esperados ou recomendados para operacao:

- `event_type`;
- `event_id`;
- `correlation_id`;
- `traceparent`;
- `tracestate`;
- `baggage`;
- `dlq_reason`, quando estiver na DLQ;
- `original_source`;
- `original_provider`;
- `original_metadata_*`, para metadados preservados do transporte original.

Para identificar `eventName` e `eventVersion`, leia `event_type` e separe o
ultimo segmento como versao. Exemplo: `LedgerEntryCreated.v2` vira
`eventName=LedgerEntryCreated` e `eventVersion=v2`.

### Decidir discard, retry ou replay

| Situacao | Decisao |
| --- | --- |
| Timeout, banco indisponivel ou erro temporario antes de processamento concluir. | `nack` e retry pelo Pub/Sub. |
| Payload invalido, schema invalido ou versao nao suportada. | Publicar DLQ de aplicacao, `ack` da original apos DLQ bem sucedida e descarte auditado. |
| Duplicidade por `payload.id`. | `ack` e registrar como sucesso idempotente. |
| DLQ causada por infraestrutura ja corrigida e contrato valido. | Redrive controlado para o topic principal, preservando payload e attributes relevantes. |
| DLQ tecnica nativa sem causa confirmada. | Manter retida para investigacao. Nao redrivar automaticamente. |
| Necessidade de recompor saldo ja aplicado com regra antiga. | Nao usar DLQ. Exige estrategia propria de rebuild ou correcao de dominio. |

## Kafka

### Componentes

| Item | Uso |
| --- | --- |
| Topic principal | `ledger.ledgerentry.created` para `LedgerEntryCreated.v1` e `LedgerEntryCreated.v2`. |
| Topico de DLQ | `ledger.ledgerentry.created.dlq`, quando o provider Kafka legado esta ativo. |
| Message key | Chave de ordenacao ou particionamento. Na DLQ Kafka, a key atual pode usar `originalTopic:originalPartition:originalOffset`. |
| Offset | Coordenada de consumo original. Serve para rastreabilidade e controle de commit, nao para idempotencia funcional. |
| Commit | Confirmacao do offset pelo consumer. So deve ocorrer apos sucesso funcional, duplicidade idempotente ou publicacao bem sucedida na DLQ. |

### Headers necessarios

Headers esperados ou recomendados para operacao:

- `event_type`;
- `event_id`;
- `correlation_id`;
- `traceparent`;
- `tracestate`;
- `baggage`;
- `dlq_reason`, quando estiver na DLQ;
- `original_topic`;
- `original_partition`;
- `original_offset`;
- `original_source`;
- `original_provider`.

Para identificar `eventName` e `eventVersion`, leia `event_type` e separe o
ultimo segmento como versao. Exemplo: `ReprocessamentoLancamentosSolicitado.v1`
vira `eventName=ReprocessamentoLancamentosSolicitado` e `eventVersion=v1`.

### Offset, commit e retry

- Se o processamento falhar por erro transitorio, nao commitar o offset original.
- Se a publicacao na DLQ falhar, nao commitar o offset original.
- Se a mensagem for invalida e a publicacao na DLQ tiver sucesso, commitar o
  offset original para evitar retry infinito.
- Se a mensagem for duplicada por `payload.id`, commitar como sucesso
  idempotente.
- Uma ferramenta futura de redrive deve controlar tambem o commit do topico de
  DLQ, sem commitar antes de validar contrato e republicar com sucesso.

### Decidir discard, retry ou replay

| Situacao | Decisao |
| --- | --- |
| `DbUpdateException`, timeout ou broker instavel. | Nao commitar offset e permitir retry do consumer. |
| Payload invalido ou versao nao suportada. | Publicar no topico de DLQ e commitar a original apenas apos DLQ bem sucedida. |
| Duplicidade por `payload.id`. | Commit como sucesso idempotente. |
| DLQ com contrato valido e causa raiz corrigida. | Redrive controlado para o topic principal, preservando payload, headers de contrato, correlacao, tracing e key adequada. |
| Mensagem operacional no consumidor financeiro. | Descarte auditado ou DLQ, conforme a politica do processor. Nao redrivar para o Balance. |
| Necessidade de replay de fatos do Ledger. | Usar fluxo de dominio apropriado, nao republicar DLQ de forma manual. |

## Observabilidade e troubleshooting

### Logs

Logs devem permitir responder:

- qual mensagem falhou;
- em qual provider;
- em qual topic, subscription, partition ou offset;
- qual `event_type` foi recebido;
- qual classe de falha foi atribuida;
- se houve `ack`, `nack`, commit, retry, DLQ ou descarte;
- qual `CorrelationId` conectou HTTP, Outbox, provider e Balance;
- se a publicacao na DLQ falhou.

Nao usar payload completo, `merchantId`, `eventId`, `correlation_id` ou offset
como labels de alta cardinalidade em sistemas de metrica. Esses valores podem
ficar no conteudo pesquisavel do log quando necessarios.

### Metricas

Metricas recomendadas:

- total de mensagens consumidas por provider, topic, event type e resultado;
- total de mensagens duplicadas por event type;
- total de mensagens enviadas para DLQ por reason e event type;
- falhas de publish na DLQ;
- backlog da Outbox em `Pending` e `DeadLetter`;
- contagem de mensagens em DLQ de aplicacao;
- contagem de mensagens em DLQ tecnica Pub/Sub, quando habilitada;
- duracao de processamento e publicacao por resultado;
- taxa de `nack` no Pub/Sub;
- taxa de retries por nao commit no Kafka.

Tags devem ser de baixa cardinalidade, como `provider`, `topic`, `event_type`,
`reason`, `result` e `error_type`.

### Correlation id e tracing

Use `X-Correlation-Id` como identificador operacional. O mesmo valor deve ser
propagado em `correlation_id` nos attributes Pub/Sub ou headers Kafka quando
existir. Use `traceparent`, `tracestate` e `baggage` para analise temporal com
OpenTelemetry quando disponiveis.

`CorrelationId` nao substitui `TraceId`. Em incidentes, use ambos:

- `CorrelationId` para conectar operacao de negocio, logs, Outbox e mensagens;
- `TraceId` para analisar latencia, spans e causalidade tecnica.

### Alertas

Alertas recomendados:

- DLQ de aplicacao maior que zero por janela sustentada;
- crescimento rapido da contagem de mensagens em DLQ;
- falha ao publicar na DLQ;
- backlog crescente da Outbox;
- mensagens `DeadLetter` na Outbox;
- taxa anormal de `nack` Pub/Sub;
- taxa anormal de retries Kafka;
- queda de consumo sem queda de publicacao;
- aumento de duplicidade alem do baseline esperado.

Cada alerta deve apontar para o runbook correto: requeue da Outbox, investigacao
de DLQ de aplicacao, investigacao de DLQ tecnica Pub/Sub ou replay de dominio.

## Checklist operacional

1. Identifique provider, origem e tipo de DLQ.
2. Colete campos minimos de investigacao.
3. Derive `eventName` e `eventVersion` a partir de `event_type`.
4. Valide contrato da versao informada.
5. Classifique a falha.
6. Confirme se a causa raiz foi corrigida.
7. Decida `discard`, retry ou replay/redrive.
8. Preserve `payload.id`, correlacao e tracing em qualquer redrive.
9. Registre decisao, operador ou automacao, horario e resultado.
10. Verifique logs, metricas, DLQ e efeito no Balance.

## Referencias

- [Replay e DLQ orientados por contrato](event-replay-and-dlq.md)
- [Operacao do Pub/Sub](pubsub.md)
- [Mensageria, Outbox e DLQ](../development/kafka-outbox.md)
- [Contratos logicos de eventos](../events/README.md)
- [Versionamento de contratos de eventos](../development/event-contract-versioning.md)
- [Observabilidade e operacao minima](../observability.md)
- [Diagnostico de replay, DLQ e projecao](../reports/replay-dlq-projection-diagnostics.md)
