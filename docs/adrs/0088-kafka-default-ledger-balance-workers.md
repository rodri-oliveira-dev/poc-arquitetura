# ADR-0088: Kafka como default de mensageria dos workers principais

## Status
Aceito

## Data
2026-06-18

## Contexto
A implementacao Kafka ja cobre o caminho principal entre `LedgerService.Worker`
e `BalanceService.Worker`, incluindo publicacao da Outbox, consumo de
`LedgerEntryCreated`, DLQ de aplicacao e o consumer de reprocessamento do
Ledger. Pub/Sub continua implementado, mas nao possui paridade para todos os
fluxos assincronos atuais, especialmente reprocessamento.

A ADR-0078 promoveu Pub/Sub a provider principal durante a fase de avaliacao do
emulator local e da integracao GCP. O diagnostico atual mostrou que, para os
servicos principais da POC, o default operacional deve voltar a ser Kafka,
mantendo Pub/Sub disponivel apenas por selecao explicita.

Esta mudanca nao e uma recomendacao generica de "Kafka e melhor". Ela atende a
um requisito explicito do projeto: a documentacao, a stack local, os workers, os
smokes k6 e a Saga do `TransferService` devem convergir para Kafka como caminho
padrao implementado e operavel hoje.

## Decisao
Kafka passa a ser o default de mensageria dos workers principais:

- quando `Messaging:Provider` esta ausente, `LedgerService.Worker` resolve
  `Kafka`;
- quando `Messaging:Provider` esta ausente, `BalanceService.Worker` resolve
  `Kafka`;
- os `appsettings.json` versionados dos dois workers usam
  `Messaging:Provider=Kafka`;
- os `appsettings.Local.example.json` dos dois workers usam
  `Messaging:Provider=Kafka`;
- os bootstrap servers para execucao no host usam a porta publicada do Kafka
  local, `127.0.0.1:19092`;
- em containers, a configuracao Kafka usa `kafka:9092` no compose padrao;
- Kafka, `kafka-init-topics`, `ledger-worker`, `balance-worker` e
  `transfer-worker` sobem no fluxo local padrao;
- `transfer-worker` sai do profile legado porque a Saga do `TransferService`
  usa Kafka explicitamente; como consequencia, Sagas podem ser processadas
  automaticamente no ambiente local padrao;
- Pub/Sub permanece implementado e suportado somente quando configurado
  explicitamente com `Messaging:Provider=PubSub`, incluindo os arquivos
  `appsettings.PubSub.json`, `compose.pubsub.yaml`, profile `legacy-pubsub` e
  scripts `scripts/local/start-stack-pubsub.*`.

Provider invalido continua falhando cedo na composition root com mensagem
objetiva. Esta decisao nao altera contratos HTTP, contratos de eventos,
migrations, regras de dominio nem acopla `Application` ou `Domain` a Kafka.

A decisao e coerente com a [ADR-0087](./0087-saga-orquestrada-transfer-service-kafka.md):
o `TransferService` usa Kafka explicitamente para os eventos da Saga e Pub/Sub
permanece fora desse fluxo.

## Consequencias

### Beneficios
- O comportamento default passa a refletir o provider com cobertura completa
  dos fluxos atuais dos workers.
- O caminho Kafka funciona sem exigir que `Messaging:Provider` esteja sempre
  presente nas configuracoes.
- Pub/Sub continua disponivel para comparacao, legado, estudos GCP e execucoes
  explicitamente configuradas.

### Custos e limitacoes
- Documentos e scripts operacionais que executam Pub/Sub devem ser tratados
  como fluxos explicitos/legados quando selecionarem `Messaging:Provider=PubSub`.
- O modo Pub/Sub passa a ser opt-in e nao sobe `TransferService.Worker`.
- A stack local padrao consome mais recursos que o modo Pub/Sub legado porque
  inclui Kafka e o worker de Saga.

### Impactos operacionais
- `LedgerService.Worker`: publica Outbox em Kafka por default, processa estornos
  e consome reprocessamento via Kafka; Pub/Sub pode publicar eventos financeiros
  apenas quando selecionado explicitamente.
- `BalanceService.Worker`: consome `ledger.ledgerentry.created` em Kafka por
  default, preserva idempotencia e publica DLQ Kafka; Pub/Sub fica restrito ao
  caminho legado/alternativo.
- `TransferService.Worker`: permanece Kafka-only para processamento de Saga,
  publicacao da Outbox e DLQ `transfer.transferencia.dlq`.
- Compose local: `compose.yaml` e `scripts/local/start-stack.*` sobem Kafka,
  `kafka-init-topics`, APIs e workers principais; `compose.pubsub.yaml` e
  `scripts/local/start-stack-pubsub.*` documentam o modo Pub/Sub legado.
- Testes: testes de composicao devem preservar Kafka como fallback quando
  `Messaging:Provider` esta ausente e manter Pub/Sub apenas como selecao
  explicita.
- Smoke/load tests: os runners k6 oficiais usam Kafka (`smoke-kafka`,
  `load-kafka`, `transfer-*-kafka`) e falham cedo para Pub/Sub porque nao ha
  runner Pub/Sub versionado.
- Operacao local: troubleshooting, observabilidade e validacao ponta a ponta
  devem partir do fluxo `Ledger -> Outbox -> Kafka -> Balance`; Pub/Sub deve
  ser descrito como validacao manual/legada.

## Substitui
- ADR-0078 no ponto especifico sobre provider principal/default dos workers
  `LedgerService` e `BalanceService`.

## Fora do escopo
- Remover adapters Pub/Sub.
- Reimplementar Kafka.
- Alterar contratos HTTP ou eventos.
