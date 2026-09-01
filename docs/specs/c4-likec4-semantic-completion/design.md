# Design - C4/LikeC4 Semantic Completion

## Decisoes

### AuditService

`AuditRecordRequestedConsumerService`, `AuditRecordRequestedProcessor` e `KafkaAuditRecordDeadLetterPublisher` sao componentes implementados do `AuditService.Worker`. Eles podem ser opcionais por configuracao (`AuditService:Worker:Enabled` e `Kafka:AuditRecordRequestedConsumer:Enabled`), mas nao sao futuros.

Continuam futuros apenas producers reais de `AuditRecordRequested.v1` em Ledger, Balance, Transfer, Payment ou outras integracoes ainda nao implementadas.

### Deployment

`localDeployment` foi substituida por views especificas:

- `localCoreDeployment`: runtime principal do `compose.yaml` sem profiles/overlays.
- `localNginxDeployment`: overlay `compose.nginx.yaml`.
- `localObservabilityDeployment`: overlay `compose.observability.yaml`.
- `localLegacyPubSubDeployment`: modo alternativo `legacy-pubsub`, com Kafka/Audit Worker fora do cenario.

### Componentes E Assemblies

Foi escolhida a Estrategia B:

- component views continuam focadas em runtime de um container especifico;
- `TransferService.Application/Domain/Infrastructure`, `PaymentService.Application/Domain/Infrastructure` e `AuditService.Application/Domain/Infrastructure` passaram a ser `codeModule`;
- views `transferCodeDependencies`, `paymentCodeDependencies` e `auditCodeDependencies` mostram dependencias de build e reutilizacao de `Shared`;
- dynamic views foram ajustadas para apontar para componentes de runtime quando descrevem execucao.

Essa decisao evita duplicar artificialmente todo o codigo, evita transformar assemblies em processos e preserva a informacao de que API e Worker reutilizam bibliotecas compartilhadas.

## Relacoes

Relações de runtime foram ajustadas para componentes como handlers, persistence adapters, workers, processors, gateways e producers/consumers.

Relações de build foram rotuladas como `build` e usadas apenas nas views de dependencias de codigo. Relações de implementacao de portas foram descritas como `Implementa portas`, sem sugerir dependencia runtime de Application para Infrastructure.

## Automacao De Consistencia

Foi avaliada automacao simples para detectar divergencias. Nesta etapa, nao foi criada automacao adicional porque LikeC4 ja valida referencias inexistentes e os riscos restantes exigiriam regras semanticas propensas a falso positivo, como inferir implementacao atual por nomes de arquivos. A recomendacao e evoluir isso com uma regra deterministica pequena se o modelo crescer ou se novas divergencias recorrentes aparecerem.
