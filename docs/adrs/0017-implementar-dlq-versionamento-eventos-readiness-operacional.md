# ADR-0017: Implementar DLQ, versionamento de eventos e readiness operacional

## Status
Aceito

## Data
2026-04-24

## Contexto
O fluxo Ledger -> Kafka -> Balance ja usava Outbox e consumo assincrono, mas ainda havia riscos operacionais relevantes:

- mensagens invalidas podiam prender o consumer no mesmo offset;
- o evento `LedgerEntryCreated` nao tinha versao explicita no `event_type`;
- headers Kafka de rastreabilidade eram publicados, mas o consumer nao os validava/tratava de forma clara;
- `/health` era usado como liveness simples, sem separar readiness;
- o fetch de JWKS nao tinha timeout/retry configuravel.

## Decisao
Implementar a Fase 2 com mudancas incrementais:

- versionar o contrato como `LedgerEntryCreated.v1`;
- publicar `event_type=LedgerEntryCreated.v1` e manter o topico fisico `ledger.ledgerentry.created`;
- validar `event_type` no Balance antes de processar o payload;
- preservar e consumir os headers `event_type`, `event_id`, `traceparent` e `baggage`;
- publicar mensagens invalidas em `ledger.ledgerentry.created.dlq`;
- commitar o offset original somente apos sucesso no processamento ou sucesso na publicacao da DLQ;
- nao commitar o offset original quando a publicacao para a DLQ falhar;
- criar `/ready` separado de `/health`;
- tornar timeout/retry/backoff do JWKS configuraveis por `Jwt:*`.

## Topicos Kafka
- Evento principal: `ledger.ledgerentry.created`
- DLQ: `ledger.ledgerentry.created.dlq`

## Contrato `LedgerEntryCreated.v1`
Campos obrigatorios:

- `id`
- `type`
- `amount`
- `createdAt`
- `merchantId`
- `occurredAt`
- `correlationId`

Campos opcionais:

- `description`
- `externalReference`

## Headers Kafka
Obrigatorio:

- `event_type`: deve ser `LedgerEntryCreated.v1`.

Recomendados:

- `event_id`: usado para rastreabilidade; quando ausente, o consumer usa o `id` do payload.
- `traceparent`: usado como contexto W3C quando presente.
- `baggage`: preservado para rastreabilidade quando presente.
- `correlation_id`: preservado para diagnostico e compatibilidade com a correlacao HTTP existente.

## Politica de DLQ e commit de offset
- Falha de desserializacao: publicar envelope na DLQ e, se o publish confirmar, commitar o offset original.
- Falha de validacao de contrato/header/payload: publicar envelope na DLQ e, se o publish confirmar, commitar o offset original.
- Falha nao recuperavel de processamento: publicar envelope na DLQ e, se o publish confirmar, commitar o offset original.
- Falha ao publicar na DLQ: nao commitar o offset original, permitindo nova tentativa.

O envelope da DLQ preserva payload original quando disponivel, topico/particao/offset originais, headers relevantes, motivo, tipo da excecao e timestamp.

## Politica de compatibilidade
Mudancas compativeis em `v1`:

- adicionar campos opcionais;
- manter nomes, tipos e semantica dos campos existentes;
- adicionar headers opcionais.

Mudancas incompativeis exigem nova versao, por exemplo `LedgerEntryCreated.v2`:

- remover ou renomear campos;
- mudar tipo ou semantica de campo existente;
- tornar obrigatorio um campo antes opcional;
- alterar interpretacao de `type`, `amount` ou timestamps.

Uma futura `v2` deve coexistir com `v1` durante migracao, preferencialmente com `event_type` distinto e consumer preparado para roteamento explicito por versao.

## Health e readiness
- `/health`: liveness simples, publico, nao depende de DB/Kafka.
- `/ready`: readiness operacional, publico nesta PoC, valida DB e Kafka quando `Kafka:Enabled=true`.

## Alternativas consideradas
1. Manter retry infinito no mesmo offset.
   - Simples, mas uma poison message trava a particao.
2. Usar Schema Registry.
   - Mais robusto, mas adiciona infraestrutura e complexidade acima do necessario para esta PoC.
3. Criar topico por versao ja em `v1`.
   - Util em producao, mas nesta fase o `event_type` versionado foi suficiente e reduziu mudancas no compose.

## Consequencias positivas
- Consumer deixa de travar indefinidamente em mensagens invalidas.
- Contrato de evento passa a ter versao explicita.
- Diagnostico de falhas melhora com envelope de DLQ.
- Orquestracao pode diferenciar processo vivo de instancia pronta.
- Fetch de JWKS tem politica explicita e configuravel.

## Consequencias negativas / trade-offs
- DLQ exige topico adicional e rotina operacional de analise/replay.
- O consumer passa a rejeitar mensagens sem `event_type`.
- Readiness com Kafka depende de metadata do broker e precisa de timeouts curtos para evitar cascata.
