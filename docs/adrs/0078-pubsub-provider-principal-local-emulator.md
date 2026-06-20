# ADR-0078: Pub/Sub como provider principal com emulator local

## Status
Substituido pela [ADR-0088](./0088-kafka-default-ledger-balance-workers.md) no ponto sobre provider principal/default dos workers `LedgerService` e `BalanceService`. As secoes abaixo preservam a decisao historica de 2026-06-02 e nao representam o fluxo padrao atual.

## Data
2026-06-02

## Contexto
O adapter Pub/Sub, o modulo Terraform e o smoke test controlado foram adicionados
incrementalmente conforme a ADR-0077. A configuracao local ainda iniciava Kafka
mesmo quando `Messaging:Provider=PubSub`, mantendo consumo desnecessario de
recursos e deixando ambiguo qual provider representava o caminho principal.

## Decisao
Pub/Sub passa a ser o provider principal da POC:

- o fallback de `Messaging:Provider` nos workers passa a ser `PubSub`;
- os `appsettings.json` dos workers usam `Messaging:Provider=PubSub` e
  `PubSub:Enabled=true`;
- `compose.yaml` inclui o emulator e os recursos locais por padrao;
- `scripts/local/start-stack.*` usa somente `compose.yaml` no fluxo Pub/Sub;
- o desenvolvimento local usa Pub/Sub emulator com projeto `poc-local`, topic
  principal `ledger.ledgerentry.created.local`, subscription
  `balance-service-ledger-events-local` e DLQ de aplicacao
  `ledger.ledgerentry.created.dlq.local`;
- `PUBSUB_EMULATOR_HOST` existe somente em configuracao local: nos containers
  aponta para `pubsub-emulator:8085` e nos profiles de debug para
  `127.0.0.1:8085`;
- Kafka permanece disponivel como opcao legada explicita por
  `Messaging:Provider=Kafka`, `compose.kafka.yaml`, profile `legacy-kafka` e
  scripts `scripts/local/start-stack-kafka.*`;
- Pub/Sub real exige configuracao explicita pelos outputs Terraform e ausencia
  de `PUBSUB_EMULATOR_HOST`.

O emulator local cria topic principal, topic de DLQ de aplicacao, subscription
principal e subscription de inspecao da DLQ de aplicacao. A DLQ tecnica nativa
continua fora do emulator local e pertence ao provisionamento real Terraform.

## Consequencias

### Beneficios
- A stack local padrao deixa de iniciar Kafka.
- O caminho principal local fica coerente com o provider adotado para GCP.
- Kafka continua utilizavel para comparacao, compatibilidade e fluxos ainda
  dependentes do adapter legado.

### Limitacoes conhecidas
- O consumer Pub/Sub de reprocessamento do Ledger ainda nao existe. Para validar
  o fluxo assincrono de reprocessamento ponta a ponta, use o modo Kafka default
  definido pela ADR-0088.
- O emulator nao reproduz integralmente limites, IAM, dead-letter policy nativa
  nem garantias do Pub/Sub real.

## Fora do escopo
- Remover codigo Kafka.
- Executar `terraform apply` ou criar recursos reais na GCP.
- Simular a DLQ tecnica nativa no emulator.
