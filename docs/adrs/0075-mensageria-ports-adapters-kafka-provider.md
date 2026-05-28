# ADR-0075: Mensageria por ports and adapters com Kafka como provider atual

## Status
Aceito

## Data
2026-05-28

## Contexto
O projeto usa Kafka hoje para publicar eventos do `LedgerService` e consumir esses eventos no `BalanceService`, com Outbox, DLQ e consumidores idempotentes. As etapas anteriores ja aproximaram publicacao, consumo e DLQ de portas mais neutras, como `IOutboxMessagePublisher`, `ReceivedMessage`, `IDeadLetterPublisher` e `TransportMessageContext`.

Ao mesmo tempo, existe interesse em permitir outro provider no futuro, como Pub/Sub, sem implementar essa integracao agora e sem quebrar a operacao local baseada em Kafka.

## Decisao
Aplicar Ports and Adapters no boundary de mensageria dos workers:

- `LedgerService.Worker` expoe `AddLedgerMessaging(configuration, environment)` na composition root.
- `BalanceService.Worker` expoe `AddBalanceMessaging(configuration, environment)` na composition root.
- A configuracao neutra `Messaging:Provider` define o provider de mensageria e usa `Kafka` como valor padrao.
- Kafka permanece o unico provider implementado e continua usando as configuracoes atuais em `Kafka:*`.
- `Kafka:Enabled=false` continua suportado para compatibilidade com testes e cenarios que desligam os hosted services de mensageria.
- Providers nao suportados falham cedo com erro explicito.

Os adapters Kafka continuam concentrando conceitos especificos de transporte, como topico, partition, offset, key e commit. Esses detalhes nao devem vazar para processors neutros nem para regras de Application/Domain. O `TransportMessageContext` pode carregar metadados tecnicos quando necessario, mas o processamento deve depender do contrato logico da mensagem, headers/attributes relevantes e idempotencia.

## Kafka como adapter atual
Kafka continua sendo a implementacao concreta nesta POC:

- publica mensagens da Outbox pelo `IOutboxMessagePublisher`;
- consome eventos do Ledger e mapeia o transporte para `ReceivedMessage`;
- publica mensagens invalidas ou nao recuperaveis pela porta `IDeadLetterPublisher`;
- preserva headers de correlacao, rastreamento, tipo de evento e idempotencia.

Topicos, contratos de eventos, politica de commit, DLQ e semantica de Outbox nao mudam nesta decisao.

## Pub/Sub futuro
Pub/Sub e apenas um adapter futuro possivel e fica fora do escopo desta decisao. A arquitetura nao deve tentar esconder diferencas semanticas entre Kafka e Pub/Sub com uma abstracao generica como `IMessageBroker`.

Uma implementacao futura devera tratar explicitamente ack/nack, delivery attempts, ordering keys, dead-letter topics/subscriptions, atributos e limites do provider, em vez de simular offset/partition/commit onde esses conceitos nao existem.

## Consequencias

### Beneficios
- A composition root passa a expressar o boundary de mensageria, nao apenas Kafka.
- Uma migracao futura fica mais localizada nos workers e adapters.
- A configuracao `Messaging:Provider` cria um ponto de extensao explicito sem remover compatibilidade com `Kafka:*`.

### Trade-offs / riscos
- Ha risco de abstracao falsa se o projeto tentar igualar Kafka e Pub/Sub alem das portas realmente usadas.
- Kafka continua presente nos nomes de metricas, options e documentacao operacional enquanto for o adapter concreto.
- Uma troca futura exigira testes de contrato e integracao para confirmar Outbox, idempotencia, DLQ, tracing e semantica de retry.

## Validacao esperada
Mudancas nesse boundary devem validar:

- build da solution;
- testes unitarios e de arquitetura dos workers;
- testes de integracao da Outbox quando houver impacto em publicacao;
- testes de consumo do Balance quando houver impacto no consumer ou DLQ.
