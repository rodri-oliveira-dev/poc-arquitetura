# Estrategia operacional de replay seguro

Este documento define a estrategia operacional para replay seguro de eventos. O
replay e uma acao intencional, auditada e limitada, executada para reprocessar
mensagens ou fatos depois de investigacao. Ele nao cria endpoint, script,
workflow, contrato novo ou mecanismo automatico.

Pub/Sub e o provider principal. Kafka permanece como provider legado opcional.
O payload logico do evento deve continuar identico entre providers; a diferenca
fica nos metadados de transporte, como attributes, headers, ordering key,
message key, ack, nack, commit, partition e offset.

## Retry, redrive e replay

| Operacao | Intencao | Momento | Exemplo |
| --- | --- | --- | --- |
| Retry automatico | Tentar novamente uma falha recuperavel sem decisao manual. | Durante publicacao ou consumo normal. | Pub/Sub `nack`, redelivery por ack deadline, Kafka sem commit de offset ou Outbox com backoff. |
| Redrive de DLQ | Retirar uma mensagem isolada em DLQ depois de classificacao e validacao. | Depois que a mensagem ja foi rejeitada no consumo. | Republicar uma mensagem valida da DLQ para o topic principal apos corrigir infraestrutura. |
| Replay seguro | Reprocessar intencionalmente uma mensagem, filtro ou conjunto de fatos com escopo, dry-run e auditoria. | Depois de investigacao operacional e avaliacao de risco. | Reconstruir uma projecao de saldo a partir de eventos validos do Ledger. |

Retry automatico nao deve depender de solicitante humano e nao deve alterar
escopo funcional. Replay seguro exige decisao operacional explicita, filtros
controlados, validacao de contrato, idempotencia e registro de resultado.

## Principios

- Replay e operacao excepcional, nao rotina de consumo.
- Replay deve preservar o payload logico, `eventId`, `eventName`,
  `eventVersion`, correlacao, tracing e chaves de ordenacao quando aplicavel.
- Replay nao deve editar payload invalido para faze-lo passar como evento
  historico.
- Replay nao deve bypassar consumers, processors, handlers idempotentes ou
  validacoes de contrato.
- Replay em lote deve ter limite, janela, filtro explicito, criterio de parada
  e dry-run anterior.
- Replay de fatos financeiros deve partir da fonte de verdade correta. DLQ e
  evidencia operacional; Outbox e mecanismo de publicacao; nenhum dos dois
  substitui o modelo de dominio do Ledger.
- Duplicidade e esperada porque Pub/Sub e Kafka operam em at-least-once.
- Para `LedgerEntryCreated`, a idempotencia funcional do Balance depende de
  `payload.id`, nao de message id Pub/Sub, offset Kafka ou id tecnico da
  Outbox.

## Tipos de replay

| Tipo | Uso | Cuidados |
| --- | --- | --- |
| Mensagem unica | Reprocessar uma mensagem identificada por `eventId`, coordenada de transporte ou registro de Outbox. | Validar contrato e confirmar que a falha anterior foi corrigida. |
| Por periodo | Reprocessar eventos dentro de uma janela temporal. | Usar janela fechada, timezone conhecido e limite de volume. |
| Por `merchantId` | Reprocessar eventos de um merchant afetado. | Confirmar autorizacao operacional e impacto nos saldos do merchant. |
| Por `accountId` | Reprocessar eventos de uma conta afetada. | Confirmar que a conta pertence ao merchant esperado e que a projecao aceita duplicatas. |
| Por `eventName` | Reprocessar uma familia logica de evento, como `LedgerEntryCreated`. | Validar que o consumidor alvo suporta esse evento. |
| Por `eventVersion` | Reprocessar uma versao especifica, como `v1` ou `v2`. | Confirmar suporte da versao e compatibilidade com schema. |
| Replay de DLQ | Reprocessar mensagem isolada pela DLQ de aplicacao ou tecnica. | Tratar como redrive controlado quando a origem for DLQ, com validacao antes de republicar. |
| Replay de Outbox | Reprocessar publicacao que falhou antes da entrega ao broker. | Diferenciar requeue de Outbox de replay de fato ja consumido. |
| Reconstrucao de projecao | Reconstruir uma leitura materializada, como saldos do Balance. | Preferir fonte de verdade do dominio e mecanismo dedicado de rebuild, nao DLQ manual. |

## Pre-condicoes

Antes de qualquer replay, confirme:

- `eventId` presente e rastreavel;
- `eventName` presente ou derivavel de `event_type`;
- `eventVersion` presente ou derivavel de `event_type`;
- payload valido contra o schema da versao informada;
- `idempotencyKey` presente quando o fluxo original exigir idempotencia HTTP ou
  operacional;
- consumidor alvo capaz de tratar a versao do evento;
- falha anterior classificada como transitoria corrigida, bug corrigido,
  configuracao corrigida, duplicidade esperada ou erro permanente;
- risco operacional avaliado, incluindo volume, impacto financeiro,
  concorrencia com consumo normal, ordenacao e efeito em projecoes;
- correlacao e tracing preservaveis ou justificadamente ausentes;
- resultado esperado definido antes da execucao.

## Quando nao fazer replay

Nao execute replay quando:

- payload e invalido;
- evento nao possui identificacao confiavel;
- `eventName`, `eventVersion` ou `event_type` estao ausentes e nao podem ser
  recuperados de fonte confiavel;
- versao e desconhecida ou nao suportada pelo consumidor;
- duplicidade nao e controlada pelo consumidor;
- erro anterior e regra de negocio permanente;
- idempotencia esta ausente no fluxo afetado;
- causa raiz ainda nao foi corrigida;
- replay exigiria edicao manual do payload original;
- filtros sao amplos demais para estimar impacto;
- nao ha observabilidade suficiente para confirmar resultado.

Nesses casos, prefira descarte auditado, evento corretivo formal, correcao de
contrato, nova decisao arquitetural ou rebuild planejado da projecao.

## Dry-run obrigatorio

Replay em lote deve executar dry-run antes da efetivacao. Para mensagem unica,
o dry-run pode ser uma etapa de validacao registrada no mesmo relatorio.

O dry-run deve informar:

- quantas mensagens seriam reprocessadas;
- quantas mensagens sao validas;
- quantas mensagens sao invalidas;
- quantas mensagens ja foram processadas de forma idempotente;
- quantas mensagens seriam ignoradas;
- quais riscos foram encontrados;
- quais filtros foram aplicados;
- quais event names e event versions apareceram no conjunto;
- quais providers, topics, subscriptions, partitions ou offsets seriam
  afetados;
- se ordering key Pub/Sub ou message key Kafka seriam preservadas;
- se ha eventos fora do contrato esperado para o filtro.

Resultado de dry-run nao autoriza replay automaticamente. A decisao final deve
ser registrada com solicitante, motivo e escopo aprovado.

## Idempotencia

Replay seguro depende de idempotencia no destino.

- Preserve `payload.id` em `LedgerEntryCreated`.
- Preserve `idempotencyKey` quando ela fizer parte do fluxo operacional.
- Nao gere novo identificador apenas para forcar reprocessamento.
- Trate duplicidade ja aplicada como resultado esperado, nao como erro.
- Diferencie id tecnico de transporte de id funcional. Message id Pub/Sub,
  offset Kafka e id da Outbox servem para rastreabilidade, nao para deduplicacao
  funcional do Balance.
- Antes de replay em lote, confirme se a tabela ou estrutura de eventos
  processados esta disponivel e consistente.
- Se o consumidor nao puder garantir idempotencia, nao execute replay.

## Contratos de eventos

Use `event_type` para derivar `eventName` e `eventVersion`. O ultimo segmento
representa a versao.

| `event_type` | `eventName` | `eventVersion` |
| --- | --- | --- |
| `LedgerEntryCreated.v1` | `LedgerEntryCreated` | `v1` |
| `LedgerEntryCreated.v2` | `LedgerEntryCreated` | `v2` |
| `LancamentoEstornoSolicitado.v1` | `LancamentoEstornoSolicitado` | `v1` |
| `ReprocessamentoLancamentosSolicitado.v1` | `ReprocessamentoLancamentosSolicitado` | `v1` |

Regras:

- validar o payload logico contra o JSON Schema da versao informada;
- nao inferir versao apenas pelo formato do payload quando `event_type` estiver
  ausente;
- manter attributes Pub/Sub e headers Kafka como metadados de transporte;
- preservar `correlation_id`, `traceparent`, `tracestate` e `baggage` quando
  existirem;
- rejeitar replay de evento operacional para consumidor financeiro incorreto;
- registrar qualquer decisao de compatibilidade antes de reprocessar evento de
  versao antiga.

## Pub/Sub

### Obter mensagem

Para mensagem unica, obtenha a mensagem pela subscription apropriada, como a
subscription principal, a subscription de inspecao da DLQ de aplicacao ou a DLQ
tecnica nativa quando existir. Para filtro, use uma fonte rastreavel que permita
listar mensagens candidatas sem confirmar entrega antes da validacao.

Nao trate message id do Pub/Sub como idempotencia funcional. Use-o apenas para
auditoria e para localizar a entrega.

### Validar attributes

Verifique attributes antes do payload:

- `event_type`;
- `event_id`;
- `correlation_id`, quando existir;
- `traceparent`, `tracestate` e `baggage`, quando existirem;
- `dlq_reason`, quando a origem for DLQ;
- `original_source`;
- `original_provider`;
- `original_metadata_*`, quando a mensagem vier de envelope de DLQ.

`event_type` deve permitir derivar `eventName` e `eventVersion`. Attributes
ausentes ou inconsistentes tornam o replay inseguro, salvo se uma fonte de
verdade confiavel puder recompor o metadado sem alterar a semantica.

### Validar payload

Valide o `Data` como payload logico do evento. Quando a mensagem estiver em DLQ
de aplicacao com envelope, extraia o payload original antes da validacao.

O payload deve ser JSON valido, respeitar o schema da versao informada e manter
os identificadores funcionais usados pela idempotencia.

### Decidir ack, nack, discard ou redrive

| Situacao | Decisao |
| --- | --- |
| Falha transitoria durante inspecao ou dry-run. | `nack` ou manter pendente, conforme a ferramenta usada. |
| Mensagem valida, causa raiz corrigida e replay aprovado. | Redrive controlado para o topic principal. |
| Mensagem invalida, versao desconhecida ou erro permanente. | Discard auditado depois de preservar evidencia. |
| Mensagem ja aplicada idempotentemente. | `ack` como resultado esperado e registrar duplicidade. |
| DLQ tecnica sem causa de entrega corrigida. | Nao redrivar. Manter para investigacao. |

So confirme a mensagem de origem depois que a decisao operacional tiver sido
registrada. Em redrive, confirme a origem apenas depois da republicacao bem
sucedida ou do descarte auditado.

### Preservar ordering key

Quando o fluxo usar ordenacao, preserve a ordering key original. Se ela precisar
ser recalculada, use a mesma regra do produtor original, normalmente derivada do
agregado, como `AggregateId` sem hifens nos eventos operacionais documentados.

Nao troque ordering key para acelerar replay. Isso pode quebrar ordenacao por
agregado e alterar o resultado observado pelo consumidor.

## Kafka

### Obter mensagem

Para mensagem unica, use topic, partition e offset, ou metadados `original_*`
quando a mensagem estiver em DLQ. Para filtro, leia o intervalo controlado de
offsets ou uma fonte persistida que preserve headers e value originais.

Offset Kafka e coordenada de transporte. Ele nao e idempotency key funcional.

### Validar headers

Verifique headers antes do payload:

- `event_type`;
- `event_id`;
- `correlation_id`, quando existir;
- `traceparent`, `tracestate` e `baggage`, quando existirem;
- `dlq_reason`, quando a origem for DLQ;
- `original_topic`;
- `original_partition`;
- `original_offset`;
- `original_source`;
- `original_provider`.

`event_type` deve permitir derivar `eventName` e `eventVersion`. Headers
ausentes ou inconsistentes tornam o replay inseguro, salvo recomposicao por
fonte confiavel e registrada.

### Validar payload

Valide o `Value` como payload logico do evento. Quando a mensagem estiver em DLQ
com envelope, extraia o payload original antes da validacao.

O payload deve ser JSON valido, respeitar o schema da versao informada e manter
os identificadores funcionais usados pela idempotencia.

### Decidir replay ou descarte

| Situacao | Decisao |
| --- | --- |
| Falha transitoria no consumo original. | Nao commitar offset original e permitir retry do consumer. |
| Mensagem valida, causa raiz corrigida e replay aprovado. | Republicar no topic principal ou executar fluxo de replay aprovado. |
| Payload invalido, versao desconhecida ou erro permanente. | Descarte auditado depois de preservar evidencia. |
| Mensagem ja aplicada idempotentemente. | Commit como sucesso idempotente quando o consumer tratar a entrega. |
| DLQ sem validacao completa. | Nao republicar e nao commitar como resolvida. |

### Evitar commit indevido

Uma ferramenta futura de replay Kafka deve controlar explicitamente quando
commitar offsets lidos para replay ou DLQ. Nao commite antes de:

- validar headers e payload;
- confirmar que a causa raiz foi corrigida;
- registrar dry-run e aprovacao;
- republicar com sucesso ou registrar descarte auditado;
- confirmar que o resultado foi observado.

Se a publicacao de redrive falhar, nao commite o offset da DLQ como resolvido.

### Preservar message key

Preserve a message key original quando ela representar particionamento ou
ordenacao por agregado. Se a key precisar ser recalculada, use a mesma regra do
produtor original.

Nao use `originalTopic:originalPartition:originalOffset` como key do topic
principal se isso mudar a ordenacao funcional. Esse formato e util para
rastreabilidade de DLQ, nao para reprocessamento no fluxo financeiro.

## Replay de DLQ

Replay de DLQ deve seguir a estrategia de DLQ antes desta estrategia de replay.
A mensagem em DLQ so pode voltar ao fluxo principal quando:

- a falha original foi classificada;
- a causa raiz foi corrigida;
- o payload e valido;
- a versao e suportada;
- a idempotencia esta garantida;
- o dry-run foi registrado;
- o operador aprovou o escopo.

Payload invalido em DLQ deve permanecer como evidencia ou ser descartado de
forma auditada. Nao corrija o payload manualmente e republique como se fosse o
mesmo evento.

## Implementacao disponivel

O BalanceService possui replay manual de mensagem unica como caso de uso
interno de Application:

- `ManualEventReplayCommand`;
- `ManualEventReplayHandler`;
- `ManualEventReplayResult`;
- `ManualEventReplayStatus`.

Nao ha endpoint administrativo publico para replay. A execucao deve ocorrer por
uma superficie operacional interna que resolva `ISender` no mesmo composition
root do `BalanceService.Worker` e envie `ManualEventReplayCommand`.

Entrada obrigatoria:

- `payload`: JSON logico original do evento;
- `eventName`: por enquanto apenas `LedgerEntryCreated`;
- `eventVersion`: `v1` ou `v2`;
- `provider`: `PubSub`, `Kafka` ou outro identificador operacional conhecido;
- `metadata`: attributes Pub/Sub, headers Kafka ou coordenadas de DLQ;
- `reason`: motivo operacional do replay.

Resultado explicito:

- `Replayed`;
- `SkippedAlreadyProcessed`;
- `RejectedInvalidContract`;
- `RejectedUnsupportedVersion`;
- `FailedProcessing`.

Exemplo de chamada interna:

```csharp
ManualEventReplayResult result = await sender.Send(
    new ManualEventReplayCommand(
        payload,
        "LedgerEntryCreated",
        "v2",
        "PubSub",
        metadata,
        "replay manual apos investigacao da DLQ"),
    cancellationToken);
```

O handler registra log estruturado com `eventId`, `eventName`,
`eventVersion`, `replayId`, `reason`, `result`, `provider` e `metadata`.

### Protecoes aplicadas

Antes de reprocessar, o handler:

1. valida `eventName`, `eventVersion` e `payload` com o catalogo de JSON
   Schemas versionados usado pelos testes de contrato;
2. rejeita versao desconhecida como `RejectedUnsupportedVersion`;
3. rejeita payload sem JSON valido, schema invalido, evento nao suportado ou
   payload nao desserializavel como `RejectedInvalidContract`;
4. extrai `payload.id` como id funcional do evento;
5. consulta `processed_events` para identificar evento ja aplicado;
6. chama `ApplyLedgerEntryCreatedCommand`, que tambem tenta inserir
   `processed_events` com `ON CONFLICT DO NOTHING` antes de alterar saldo.

Essa dupla verificacao protege a execucao manual contra duplicidade observada
antes do replay e contra corrida com consumo normal. Se a corrida acontecer, o
handler de aplicacao retorna duplicidade e o replay termina como
`SkippedAlreadyProcessed`.

### Pub/Sub para replay manual

Para uma mensagem unica vinda do Pub/Sub:

1. leia a mensagem na subscription de inspecao da DLQ de aplicacao ou na
   subscription da DLQ tecnica nativa, quando existir;
2. se a mensagem estiver no envelope `DeadLetterMessage`, extraia o payload
   original preservado no envelope;
3. derive `eventName` e `eventVersion` de `event_type`, ou use os campos
   equivalentes preservados no envelope;
4. passe attributes originais, `dlq_reason`, `original_source`,
   `original_provider`, `original_metadata_*`, ordering key e message id em
   `metadata`;
5. execute o caso de uso interno;
6. confirme a mensagem de origem somente depois de registrar o resultado
   operacional.

O replay manual nao republica automaticamente no topic principal. Ele chama o
mesmo caso de uso idempotente de aplicacao do Balance. Se for necessario
redrive por republicacao no Pub/Sub, isso continua sendo uma decisao
operacional separada.

### Kafka para replay manual

Para uma mensagem unica vinda do topico de DLQ Kafka:

1. leia a mensagem por topic, partition e offset da DLQ;
2. se o value estiver no envelope `DeadLetterMessage`, extraia o payload
   original preservado no envelope;
3. derive `eventName` e `eventVersion` de `event_type`;
4. passe headers originais, `dlq_reason`, `original_topic`,
   `original_partition`, `original_offset`, `original_source`,
   `original_provider`, message key e coordenadas Kafka em `metadata`;
5. execute o caso de uso interno;
6. commite ou marque a mensagem da DLQ como resolvida somente depois de
   registrar o resultado operacional.

O replay manual nao remove Kafka nem altera o consumer legado. Kafka continua
podendo fornecer a mensagem candidata e seus headers, mas a protecao de
contrato e idempotencia permanece no caso de uso de Application.

## Replay de Outbox

Outbox trata publicacao antes da entrega ao broker. Requeue de uma mensagem
`DeadLetter` da Outbox corrige falha de publicacao e nao deve ser confundido
com replay de evento ja consumido.

Antes de requeue ou replay relacionado a Outbox:

- confirme que a mensagem ainda representa evento valido;
- confirme que o broker ou configuracao que causou a falha foi corrigido;
- preserve `OutboxMessage.Id`, payload, `event_type`, correlacao e tracing;
- confirme que republicar pode gerar entrega duplicada e que o consumidor e
  idempotente;
- registre resultado como requeue de Outbox, nao como replay de DLQ.

## Replay para reconstrucao de projecao

Reconstrucao de projecao deve ser planejada como rebuild operacional. Ela pode
usar filtros por periodo, `merchantId`, `accountId`, `eventName` e
`eventVersion`, mas deve partir da fonte de verdade do dominio ou de log de
eventos confiavel.

Nao use DLQ como fonte primaria para reconstruir saldo. DLQ contem falhas,
duplicidades, mensagens invalidas e evidencia parcial. Rebuild de projecao deve
ter janela, checkpoint, isolamento, comparacao de resultado e criterio de
rollback operacional.

## Auditoria

Todo replay deve gerar registro auditavel com:

- `replayId`;
- motivo;
- solicitante;
- data e horario;
- filtros usados;
- provider e origem;
- resultado do dry-run;
- resultado final;
- mensagens reprocessadas;
- mensagens ignoradas;
- mensagens invalidas;
- mensagens ja processadas;
- erros encontrados;
- decisao de `ack`, `nack`, discard, redrive, commit ou nao commit;
- links para logs, traces, metricas ou evidencias usadas na decisao.

O registro deve permitir responder quem pediu, por que pediu, o que foi
afetado, qual risco foi aceito e qual resultado foi observado.

## Checklist operacional

1. Defina objetivo, escopo e solicitante.
2. Escolha o tipo de replay.
3. Liste filtros e limite de volume.
4. Colete `eventId`, `eventName`, `eventVersion`, provider e origem.
5. Valide attributes Pub/Sub ou headers Kafka.
6. Valide payload contra o schema da versao.
7. Confirme idempotencia do consumidor.
8. Classifique a falha anterior.
9. Avalie risco operacional.
10. Execute dry-run e registre resultados.
11. Aprove ou cancele o replay.
12. Execute em lote limitado ou mensagem unica.
13. Preserve ordering key Pub/Sub ou message key Kafka.
14. Registre `ack`, `nack`, discard, redrive, commit ou nao commit.
15. Verifique logs, metricas, traces, DLQ, Outbox e projecao afetada.

## Referencias

- [Estrategia operacional de DLQ](dlq-strategy.md)
- [Replay e DLQ orientados por contrato](event-replay-and-dlq.md)
- [Operacao do Pub/Sub](pubsub.md)
- [Mensageria, Outbox e DLQ](../development/kafka-outbox.md)
- [Contratos logicos de eventos](../events/README.md)
- [JSON Schemas versionados de eventos](../../contracts/events/README.md)
- [Versionamento de contratos de eventos](../development/event-contract-versioning.md)
- [Diagnostico de replay, DLQ e projecao](../reports/replay-dlq-projection-diagnostics.md)
