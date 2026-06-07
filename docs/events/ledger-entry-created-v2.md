# LedgerEntryCreated.v2

## Identificacao

| Item | Valor |
| --- | --- |
| Nome do evento | `LedgerEntryCreated` |
| Versao | `v2` |
| `event_type` | `LedgerEntryCreated.v2` |
| Produtor | `LedgerService` |
| Consumidores | `BalanceService.Worker` por Pub/Sub ou Kafka |
| Natureza | Integracao entre servicos |
| JSON Schema versionado | [`../../contracts/events/ledger-entry-created.v2.schema.json`](../../contracts/events/ledger-entry-created.v2.schema.json) |
| Exemplos versionados | [`valido`](../../contracts/events/examples/ledger-entry-created.v2.valid.json), [`invalido`](../../contracts/events/examples/ledger-entry-created.v2.invalid.json) |

Este e o contrato atual produzido pelo Ledger para fatos financeiros finais. O payload logico e identico em Pub/Sub e Kafka. Diferencas entre topic, subscription, attributes, headers, key, ack, commit e DLQ continuam isoladas nos adapters de transporte.

## Decisao de versao

`currency` foi tratada como mudanca semanticamente relevante. Em vez de adicionar o campo em `LedgerEntryCreated.v1`, foi criado `LedgerEntryCreated.v2` com `currency` explicito e obrigatorio.

`LedgerEntryCreated.v1` continua aceito como legado. Ao consumir v1, o Balance aplica fallback documentado para `BRL`. Ao consumir v2, o Balance exige `currency` no payload e envia a mensagem para DLQ quando o campo estiver ausente ou invalido.

## Payload logico

| Campo | Obrigatorio | Tipo de dado | Semantica |
| --- | --- | --- | --- |
| `id` | Sim | string | Identificador logico estavel do lancamento, no formato `lan_` mais 8 caracteres hexadecimais. |
| `type` | Sim | string | Tipo financeiro. Valores atuais: `CREDIT` ou `DEBIT`. |
| `amount` | Sim | string decimal | Valor com duas casas decimais. Positivo para `CREDIT` e negativo para `DEBIT`. |
| `currency` | Sim | string | Moeda ISO 4217 do lancamento. Na POC atual, o Ledger produz `BRL`. |
| `createdAt` | Sim | string date-time ISO 8601 | Instante de criacao no Ledger. |
| `merchantId` | Sim | string | Merchant dono do lancamento e da projecao. |
| `occurredAt` | Sim | string date-time ISO 8601 | Instante do fato financeiro. O offset define o dia consolidado. |
| `description` | Nao | string ou null | Descricao opcional. |
| `correlationId` | Sim | string UUID | Correlacao logica do fluxo de origem. |
| `externalReference` | Nao | string ou null | Referencia externa opcional. |

## Exemplo valido

```json
{
  "id": "lan_1a2b3c4d",
  "type": "CREDIT",
  "amount": "150.00",
  "currency": "BRL",
  "createdAt": "2026-06-06T12:34:56.0000000Z",
  "merchantId": "merchant-001",
  "occurredAt": "2026-06-06T12:34:56.0000000Z",
  "description": "Venda aprovada",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
  "externalReference": "order-123"
}
```

## Exemplo invalido

```json
{
  "id": "lan_1a2b3c4d",
  "type": "CREDIT",
  "amount": "150.00",
  "createdAt": "2026-06-06T12:34:56.0000000Z",
  "merchantId": "merchant-001",
  "occurredAt": "2026-06-06T12:34:56.0000000Z",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
  "externalReference": "order-123"
}
```

Motivo: `currency` e obrigatorio em v2.

## Transporte

Pub/Sub e Kafka usam o mesmo payload logico. O `event_type` versionado fica nos attributes do Pub/Sub ou headers do Kafka, e deve ser `LedgerEntryCreated.v2`.

| Provider | Destino principal | Metadados tecnicos |
| --- | --- | --- |
| Pub/Sub | `ledger.ledgerentry.created.local` no ambiente local e `ledger.ledgerentry.created.dev` no GCP dev documentado | `event_id`, `event_type`, `correlation_id`, `traceparent`, `tracestate`, `baggage` em attributes |
| Kafka | `ledger.ledgerentry.created` | `event_id`, `event_type`, `correlation_id`, `traceparent`, `tracestate`, `baggage` em headers |

O mapeamento de `LedgerEntryCreated.v2` usa o mesmo destino fisico de `LedgerEntryCreated.v1`. Isso preserva os adapters e evita criar topicos paralelos para a mesma familia de evento.

## Compatibilidade

- Produtores novos devem emitir `LedgerEntryCreated.v2`.
- Consumidores devem aceitar `LedgerEntryCreated.v2` sem fallback de moeda.
- Consumidores podem manter leitura de `LedgerEntryCreated.v1` enquanto houver mensagens antigas ou Kafka legado.
- `LedgerEntryCreated.v1` com propriedade `currency` e invalido, porque v1 nao define esse campo.
