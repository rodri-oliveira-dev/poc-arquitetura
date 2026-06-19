# LedgerEntryCreated.v1

## Identificacao

| Item | Valor |
| --- | --- |
| Nome | `LedgerEntryCreated` |
| Versao | `v1` |
| `event_type` | `LedgerEntryCreated.v1` |
| Origem | `LedgerService` |
| Consumidor | `BalanceService.Worker` |
| Topic Pub/Sub local principal | `ledger.ledgerentry.created.local` |
| Topic Pub/Sub GCP dev | `ledger.ledgerentry.created.dev` |
| Topico Kafka | `ledger.ledgerentry.created` |
| Schema | [`LedgerEntryCreated.v1.schema.json`](LedgerEntryCreated.v1.schema.json) |
| Exemplo valido | [`LedgerEntryCreated.v1.example.json`](LedgerEntryCreated.v1.example.json) |

O evento representa um fato financeiro final persistido pelo Ledger. Lancamentos normais, compensatorios de estorno e reprocessados usam o mesmo contrato.

## Payload

| Campo | Obrigatorio | Semantica |
| --- | --- | --- |
| `id` | Sim | Identificador logico estavel derivado do lancamento. Tambem sustenta idempotencia no Balance. |
| `type` | Sim | `CREDIT` para credito ou `DEBIT` para debito. |
| `amount` | Sim | Decimal como string com duas casas; positivo para `CREDIT` e negativo para `DEBIT`. |
| `createdAt` | Sim | Instante ISO 8601 de criacao do lancamento no Ledger. |
| `merchantId` | Sim | Merchant dono do lancamento e da projecao atualizada. |
| `occurredAt` | Sim | Instante ISO 8601 do fato financeiro; seu offset define o dia consolidado. |
| `description` | Nao | Descricao opcional; pode ser `null`. |
| `correlationId` | Sim | UUID de correlacao do fluxo de origem. |
| `externalReference` | Nao | Referencia externa opcional; pode ser `null`. |

## Currency

`currency` nao faz parte de `LedgerEntryCreated.v1`. O Ledger atual nao recebe nem persiste moeda. O Balance consolida e responde `BRL` como default conhecido da POC.

Adicionar `currency` como obrigatorio quebraria consumidores `v1`. Suporte real a multiplas moedas exige decisao futura para contrato HTTP, entidade persistida no Ledger, evento e consultas do Balance. Quando isso ocorrer, a evolucao deve usar `LedgerEntryCreated.v2` ou uma migracao explicitamente documentada.

## Compatibilidade

Mudancas compativeis em `v1`:

- adicionar campo opcional;
- adicionar header opcional;
- manter nome, tipo e semantica dos campos existentes.

Mudancas incompativeis exigem nova versao:

- remover ou renomear campo;
- mudar tipo ou semantica;
- tornar obrigatorio um campo antes ausente ou opcional;
- alterar a interpretacao de `type`, `amount`, timestamps ou `currency`.

O schema usa `additionalProperties=false`. Portanto, o consumer atual rejeita campos ainda nao reconhecidos ate que schema, consumer e testes sejam atualizados juntos.
