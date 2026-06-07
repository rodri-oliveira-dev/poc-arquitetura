# Replay e DLQ orientados por contrato

Este runbook define o fluxo recomendado para inspecionar, classificar e
reprocessar mensagens de DLQ ou replay sem bypassar contratos de evento,
idempotencia ou semanticas de transporte.

Pub/Sub e o provider principal. Kafka permanece como provider legado opcional.
O contrato logico do evento e o mesmo nos dois providers: o payload JSON deve
validar contra o JSON Schema da versao correta, enquanto attributes Pub/Sub ou
headers Kafka carregam metadados tecnicos como `event_type`, `event_id`,
correlacao, tracing e diagnostico de DLQ.

## Diagnostico atual

O projeto possui tres caminhos diferentes que nao devem ser confundidos:

| Caminho | Estado atual | Observacao operacional |
| --- | --- | --- |
| Outbox do Ledger | Mensagens `DeadLetter` ficam no banco e podem voltar para `Pending` pelo endpoint administrativo de requeue. | Requeue corrige falha de publicacao, nao corrige contrato nem payload. |
| DLQ de aplicacao do Balance | Mensagens invalidas, contratos nao suportados ou falhas nao recuperaveis sao publicadas pela porta `IDeadLetterPublisher`. | Redrive deve validar o payload original antes de republicar no topic principal. |
| Replay de reprocessamento | O Ledger possui solicitacao HTTP e consumer Kafka de `ReprocessamentoLancamentosSolicitado.v1`; ele republica fatos financeiros finais no Outbox. | O consumer Pub/Sub equivalente ainda nao existe. |

No consumo do Balance, `LedgerEntryCreatedMessageProcessor` ja valida
`event_type`, diferencia `LedgerEntryCreated.v1` e `LedgerEntryCreated.v2`,
desserializa com rejeicao de propriedades desconhecidas, valida campos
obrigatorios e publica DLQ de aplicacao antes de permitir `ack` ou commit para
mensagens invalidas.

No Kafka, o offset original so deve ser commitado depois de processamento com
sucesso ou depois de publicacao bem sucedida na DLQ de aplicacao. Se a
publicacao na DLQ falhar, o commit nao deve acontecer. No Pub/Sub, o consumer
responde `Ack` para sucesso ou DLQ publicada, e `Nack` para falha recuperavel.

## Classificacao de mensagens

Antes de qualquer replay ou redrive, classifique a mensagem:

| Tipo | Exemplo | Acao recomendada |
| --- | --- | --- |
| Mensagem nova valida | `event_type=LedgerEntryCreated.v2` e payload conforme `ledger-entry-created.v2.schema.json`. | Processar pelo fluxo normal. |
| Mensagem antiga v1 valida | `event_type=LedgerEntryCreated.v1`, payload sem `currency` e conforme schema v1. | Aceitar enquanto a politica de convivencia de v1 estiver vigente. |
| Mensagem nova v2 valida | Payload v2 com `currency` obrigatoria valida. | Processar pelo fluxo normal. |
| Mensagem invalida | JSON quebrado, campo obrigatorio ausente, campo extra rejeitado ou versao incompativel com payload. | Enviar para DLQ de aplicacao, registrar motivo e nao redrivar sem correcao formal. |
| Mensagem valida mas nao processavel por regra | Contrato passa, mas regra de dominio impede aplicacao. | Enviar para DLQ de aplicacao quando a falha for nao recuperavel, ou manter retry quando for transiente. |
| Mensagem duplicada | Mesmo `payload.id` ja registrado em `processed_events`. | Preservar idempotencia e tratar como resultado esperado de entrega at-least-once. |

O identificador de idempotencia do Balance e o `id` do payload financeiro, nao o
`event_id` tecnico de transporte. Redrive e replay devem preservar esse `id`.

## Validacao de contrato

Use o `event_type` para escolher o schema:

| `event_type` | Schema |
| --- | --- |
| `LedgerEntryCreated.v1` | `contracts/events/ledger-entry-created.v1.schema.json` |
| `LedgerEntryCreated.v2` | `contracts/events/ledger-entry-created.v2.schema.json` |
| `LancamentoEstornoSolicitado.v1` | `contracts/events/lancamento-estorno-solicitado.v1.schema.json` |
| `ReprocessamentoLancamentosSolicitado.v1` | `contracts/events/reprocessamento-lancamentos-solicitado.v1.schema.json` |

Regras:

- valide somente o payload logico, nao headers, attributes, offset,
  subscription ou motivo de DLQ;
- trate ausencia de `event_type` como falha de contrato operacional;
- nao tente inferir versao apenas pelo formato do payload quando o metadado de
  transporte estiver ausente;
- para `LedgerEntryCreated.v1`, `currency` no payload e invalido;
- para `LedgerEntryCreated.v2`, `currency` e obrigatoria;
- preserve `correlationId`, `correlation_id`, `traceparent`, `tracestate` e
  `baggage` quando redrivar uma mensagem valida;
- registre motivo, operador ou automacao, horario, origem e decisao tomada.

A validacao automatizada dos schemas versionados roda com:

```bash
npm ci
npm run events:validate
```

## Fluxo recomendado para Pub/Sub

1. Inspecione a mensagem na subscription de DLQ aplicavel.
2. Diferencie a DLQ de aplicacao da DLQ tecnica nativa do Pub/Sub.
3. Leia attributes como `event_type`, `event_id`, `correlation_id`,
   `traceparent`, `tracestate`, `baggage`, `dlq_reason`, `original_source`,
   `original_provider` e `original_metadata_*`.
4. Identifique o event name e a versao a partir de `event_type`, por exemplo
   `LedgerEntryCreated` e `v2`.
5. Extraia o payload original do envelope de DLQ de aplicacao quando a mensagem
   estiver no topic de DLQ publicado pelo Balance.
6. Valide o payload contra o JSON Schema da versao correta.
7. Classifique a causa:
   - contrato invalido ou metadado obrigatorio ausente;
   - falha tecnica transiente ja corrigida;
   - regra de negocio nao recuperavel;
   - duplicidade esperada.
8. Decida:
   - `ack` para mensagem de DLQ ja inspecionada e resolvida;
   - `nack` somente quando a inspecao ou ferramenta falhou de forma
     recuperavel;
   - discard controlado quando o payload e invalido ou a regra e
     permanentemente nao processavel;
   - redrive para o topic principal somente quando o contrato for valido e a
     causa raiz estiver corrigida.
9. No redrive, publique o mesmo payload logico no topic principal, preserve
   `event_type`, `event_id` quando fizer sentido operacional, correlacao e
   tracing, e nao altere `payload.id`.
10. Confirme que o Balance tratou duplicatas por `processed_events` e registre
    a decisao operacional.

No Pub/Sub real, a DLQ tecnica nativa representa falha de entrega do transporte.
Ela nao deve ser tratada automaticamente como erro de contrato. Antes de
redrivar uma mensagem da DLQ tecnica, aplique a mesma validacao de contrato e
confirme que a falha de entrega foi corrigida.

## Fluxo recomendado para Kafka

1. Inspecione a mensagem no topico de DLQ configurado, quando existir.
2. Leia headers como `event_type`, `event_id`, `correlation_id`, `traceparent`,
   `tracestate`, `baggage`, `dlq_reason`, `original_topic`,
   `original_partition` e `original_offset`.
3. Identifique event name e versao a partir de `event_type`.
4. Extraia o payload original do envelope de DLQ de aplicacao.
5. Valide o payload contra o JSON Schema da versao correta.
6. Classifique a causa da DLQ e confirme se ela foi corrigida.
7. Decida:
   - nao commitar a mensagem original quando a publicacao na DLQ falhar;
   - nao republicar payload invalido no topic principal;
   - redrivar somente payload valido e com causa raiz corrigida;
   - descartar de forma auditada mensagens permanentemente invalidas.
8. Ao redrivar, preserve o payload logico, `event_type`, correlacao, tracing e a
   chave de ordenacao quando ela derivar do mesmo agregado.
9. Evite commit indevido: uma ferramenta futura de redrive Kafka deve controlar
   explicitamente quando o offset do topico de DLQ foi tratado, sem antecipar
   commit antes da validacao e da republicacao bem sucedida.
10. Verifique idempotencia no Balance pelo `payload.id` e use os headers
    `original_*` apenas para auditoria e rastreabilidade.

O reprocessamento assincrono do Ledger ainda depende do modo Kafka legado para
`ReprocessamentoLancamentosSolicitado.v1`. Esse fluxo valida fonte logica,
`event_type` e payload antes de chamar o caso de uso, mas ele nao substitui um
redrive generico de DLQ do Balance.

## Replay e redrive

Replay e reconstrucao intencional de fatos a partir da fonte de verdade do
dominio, como o reprocessamento de lancamentos persistidos no Ledger. Redrive e
republicacao operacional de uma mensagem que caiu em DLQ ou ficou retida por
falha de transporte.

Regras comuns:

- nunca altere payload invalido manualmente e republique como se fosse o mesmo
  evento sem decisao de correcao documentada;
- nao use requeue da Outbox para mensagens que ja foram consumidas e enviadas
  para DLQ pelo Balance;
- nao bypassar `LedgerEntryCreatedMessageProcessor` nem o handler idempotente do
  Balance;
- preserve o `payload.id` para manter deduplicacao;
- redrive em lote deve ter limite, observabilidade e criterio de parada;
- eventos operacionais como `ReprocessamentoLancamentosSolicitado.v1` nao devem
  ser redrivados para o Balance como evento financeiro.

## Pendencias funcionais

- Nao existe ferramenta versionada de redrive de DLQ Pub/Sub ou Kafka.
- Nao existe consumer Pub/Sub para `ReprocessamentoLancamentosSolicitado.v1`;
  o reprocessamento ponta a ponta continua Kafka only.
- Nao ha validador runtime compartilhado que carregue os JSON Schemas antes de
  redrive manual. A validacao atual do consumidor e por desserializacao e regras
  equivalentes no processor.
- Nao ha endpoint administrativo para redrive de DLQ de aplicacao, e este
  documento nao cria um.
- A decisao sobre ferramenta futura deve definir auditoria, permissao,
  dry-run, limite de lote, tratamento de commit ou ack e formato de relatorio.

## Referencias

- [Politica de versionamento de contratos de eventos](../development/event-contract-versioning.md)
- [Contratos logicos de eventos](../events/README.md)
- [JSON Schemas versionados](../../contracts/events/README.md)
- [Mensageria, Outbox e DLQ](../development/kafka-outbox.md)
- [Operacao do Pub/Sub](pubsub.md)
- [Diagnostico de contratos de eventos](../reports/event-contracts-diagnostics.md)
