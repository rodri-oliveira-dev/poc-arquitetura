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
- em containers, a configuracao Kafka continua usando `kafka:9092` quando
  selecionada pelo compose/overlay;
- Pub/Sub permanece implementado e suportado somente quando configurado
  explicitamente com `Messaging:Provider=PubSub`, incluindo os arquivos
  `appsettings.PubSub.json`.

Provider invalido continua falhando cedo na composition root com mensagem
objetiva. Esta decisao nao altera contratos HTTP, contratos de eventos,
migrations, regras de dominio nem acopla `Application` ou `Domain` a Kafka.

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
- Remover Pub/Sub ou mudar a stack local de compose para Kafka por padrao fica
  fora desta decisao e exige frente propria, pois impacta scripts, k6,
  troubleshooting e custo local.

## Substitui
- ADR-0078 no ponto especifico sobre provider principal/default dos workers
  `LedgerService` e `BalanceService`.

## Fora do escopo
- Remover adapters Pub/Sub.
- Reimplementar Kafka.
- Alterar contratos HTTP ou eventos.
- Trocar a topologia completa do compose local e scripts associados.
