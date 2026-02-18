# ADR-0015: (Ponto de melhoria) Governança de contratos de eventos (Kafka): versionamento, compatibilidade e DLQ

## Status
Proposto

## Data
2026-02-18

## Contexto
A PoC publica eventos (ex.: `LedgerEntryCreated`) do `LedgerService` para o `BalanceService` via Kafka (ADR-0003).

Hoje o contrato do evento é um JSON serializado (sem um mecanismo formal de versionamento/compatibilidade) e o consumer faz `JsonSerializer.Deserialize<LedgerEntryCreatedEvent>`.

Em cenários reais, evoluções do payload podem causar:

- quebra de consumidores ao adicionar/remover/renomear campos;
- dificuldade de reprocessar histórico (eventos antigos com formato anterior);
- mensagens “envenenadas” (poison messages) que travam o consumo (offset não anda) quando não são tratadas.

## Decisão
Definir uma governança mínima para eventos Kafka neste repositório:

1) **Contrato versionado por tipo de evento**
   - `event_type` deve carregar o nome **e versão** (ex.: `LedgerEntryCreated.v1`).
   - Evoluções compatíveis (adição de campos opcionais) mantêm a mesma versão.
   - Mudanças incompatíveis (remoção/renomeação/semântica) exigem versão nova (`.v2`) e, idealmente, **tópico separado** ou política clara de coexistência.

2) **Compatibilidade backward/forward documentada**
   - Para cada evento, documentar quais campos são obrigatórios e quais são opcionais.
   - Regras: somente **adicionar** campos opcionais na mesma versão; não reutilizar nomes com semântica diferente.

3) **Tratamento de poison message**
   - Quando falhar desserialização/validação do evento, **não ficar em loop infinito** no mesmo offset.
   - Adotar uma estratégia de **DLQ** (Dead Letter Queue) por tópico (ex.: `ledger.ledgerentry.created.dlq`) ou um tópico genérico com envelope.

4) **Envelope mínimo (se necessário)**
   - Caso a evolução de contratos exija, introduzir um envelope com: `event_id`, `event_type`, `occurred_at`, `payload`, `correlation_id`, `traceparent`.
   - O repositório já propaga parte disso em headers; o envelope só é necessário se quisermos persistir/reprocessar fora do Kafka.

## Consequências

### Benefícios
- Reduz risco de quebra na evolução de eventos.
- Facilita reprocessamento e coexistência de versões.
- Melhora robustez do consumer (não trava por mensagens inválidas).

### Trade-offs / custos
- Aumenta disciplina e necessidade de documentação de contrato.
- DLQ exige novos tópicos e uma rotina (mesmo que manual) de análise/replay.
- Pode exigir ajustes nos produtores/consumidores para lidar com múltiplas versões.

## Alternativas consideradas

1) **Manter JSON “sem contrato”**
   - Prós: simples.
   - Contras: quebra fácil e troubleshooting ruim.

2) **Schema Registry (Avro/Protobuf/JSON Schema)**
   - Prós: melhor governança e validação; compatibilidade automatizada.
   - Contras: adiciona infra e complexidade (pode ser demais para a PoC).

## Próximos passos (não implementados)

- TODO: decidir se `event_type` versionado será refletido no `TopicMap` (ex.: mapear `LedgerEntryCreated.v1`).
- TODO: criar tópicos DLQ no `compose.yaml` via job de init.
- TODO: ajustar `LedgerEventsConsumer` para publicar mensagem inválida na DLQ e commitar offset.
