# Report - C4/LikeC4 Semantic Completion

Este relatorio sera finalizado apos as validacoes finais e a inspecao visual dos PNGs exportados.

## Diagnostico Confirmado

### AuditService

Confirmado no codigo:

- `AuditRecordRequestedConsumerService` existe em `src/audit/AuditService.Worker/Messaging/Kafka`.
- `AuditRecordRequestedProcessor` existe e desserializa/valida/mapeia mensagens para o fluxo canonico de auditoria.
- `KafkaAuditRecordDeadLetterPublisher` existe e publica DLQ de aplicacao.
- `AuditService.Worker/DependencyInjection.cs` registra HostedService, processor e publisher quando as opcoes estao habilitadas.
- Testes em `tests/audit/AuditService.Worker.Tests` cobrem DI, consumer, processor e publisher de DLQ.
- `CreateAuditRecordCommandHandler` e repositorios do Audit cobrem persistencia e idempotencia por `Idempotency-Key`/`source_event_id`.

Impacto: `#future` nos componentes implementados fazia o leitor interpretar o Worker como roadmap, embora ele rode no Compose padrao quando habilitado.

### Deployment

Confirmado que a view antiga `localDeployment` incluia `local.**`, misturando core, Nginx, observabilidade, Pub/Sub legado, init containers e servicos desativados por overlay/profile.

Impacto: a view podia sugerir que todos os servicos executavam simultaneamente no Compose padrao.

### Component Views

Confirmado que Transfer, Payment e Audit representavam `Application`, `Domain` e `Infrastructure` como componentes no nivel do bounded context e os incluiam em component views de API. Isso misturava runtime com dependencias de build.

Impacto: API e Worker pareciam compartilhar componentes C4 de runtime, embora compartilhem assemblies/projetos.

## AuditService

Tags removidas:

- `#future` de `AuditRecordRequestedProcessor`.
- `#future` de `KafkaAuditRecordDeadLetterPublisher`.

Tags mantidas:

- `#optional` nos componentes habilitados por configuracao do Worker/consumer.

Continuam futuras apenas integracoes/producers reais de `AuditRecordRequested.v1` nos demais bounded contexts.

## Deployment Views

- `localCoreDeployment`: mostra o core do `compose.yaml` sem profiles/overlays: PostgreSQL, Kafka, Keycloak, Mailpit, APIs, Workers principais e init containers necessarios.
- `localNginxDeployment`: mostra Nginx, certificados, portal por instancia do Nginx, replicas adicionais do Ledger e servicos roteados do core.
- `localObservabilityDeployment`: mostra Collector, Jaeger, Prometheus, Loki, Alloy, Grafana, Alertmanager e APIs/Workers como fontes de telemetria.
- `localLegacyPubSubDeployment`: mostra emulator, init, Ledger Worker e Balance Worker com `Messaging:Provider=PubSub`; documenta que Kafka/kafka-init sao removidos e Transfer/Audit Workers ficam desativados nesse modo.

## Component Views

Transfer, Payment e Audit foram ajustados para component views de runtime:

- APIs mostram superficie HTTP, handlers/servicos principais, adapters de persistencia e externos relevantes.
- Workers mostram composition roots, HostedServices, processors, gateways, publishers/consumers, DLQ e persistencia.
- Assemblies compartilhados foram removidos das component views de runtime.

Ledger, Balance e Identity ja estavam mais proximos dessa abordagem e foram preservados para evitar churn fora do escopo.

## Dependencias De Codigo

Estrategia escolhida: Estrategia B.

Views criadas:

- `transferCodeDependencies`
- `paymentCodeDependencies`
- `auditCodeDependencies`

Nelas, `Application`, `Domain`, `Infrastructure` e `PocArquitetura.Shared` aparecem como `codeModule` com relacoes `build`.

## Relacoes

Relações runtime corrigidas:

- API -> handlers/query handlers.
- Handlers -> persistence adapters.
- Persistence adapters -> schema PostgreSQL.
- Worker -> HostedServices/processors/gateways/producers/consumers.
- Gateway/adapters -> Stripe, Ledger, Kafka ou Keycloak.

Relações de build separadas:

- API/Worker -> Application.
- Application -> Domain.
- Infrastructure -> Application/Domain.
- API/Worker -> Infrastructure.
- API/Worker -> Shared.

## Inspecao Visual

Views PNG geradas e inspecionadas:

- `systemLandscape`
- `businessContainers`
- `identityServiceContainers`
- `ledgerContainers`
- `balanceContainers`
- `transferContainers`
- `paymentContainers`
- `auditContainers`
- `localCoreDeployment`
- `localNginxDeployment`
- `localObservabilityDeployment`
- `localLegacyPubSubDeployment`
- `transferApiComponents`
- `transferWorkerComponents`
- `paymentApiComponents`
- `paymentWorkerComponents`
- `auditApiComponents`
- `auditWorkerComponents`
- `auditKafkaIngestionFlow`
- `transferCodeDependencies`
- `paymentCodeDependencies`
- `auditCodeDependencies`

Resultado:

- Container views de bounded context ficaram legiveis para drill-down.
- `systemLandscape`, `businessContainers`, `integrationContainers` e
  `observabilityFlow` continuam densas, mas funcionam como referencia geral.
- `localCoreDeployment` ficou separado de Nginx, observabilidade e Pub/Sub.
- `localLegacyPubSubDeployment` foi ajustada durante a inspeção para explicitar
  topic principal, subscription, DLQ de aplicacao e DLQ tecnica documentada.
- Component views de Transfer, Payment e Audit nao mostram mais assemblies
  compartilhados como componentes de runtime.
- Views `*CodeDependencies` distinguem build/runtime por titulo, descricao,
  tag `code` e relacoes `build`.

Artefatos PNG/JSON foram gerados para validacao e removidos da arvore de
trabalho porque sao saida reproduzivel, nao fonte versionada.

## Validacoes

Comandos executados:

- `npm ci`: sucesso. Aviso npm sobre `esbuild@0.28.1` possuir script
  `postinstall` ainda nao aprovado por `npm approve-scripts`.
- `npx likec4 validate docs/architecture`: sucesso.
- `npm run architecture:build`: sucesso. Na ultima execucao houve aviso
  informativo `[PLUGIN_TIMINGS]` do Rolldown/Vite, sem falha.
- `npx likec4 export json`: sucesso; gerou `likec4.json`.
- `npx likec4 export png`: sucesso; exportou 43 views.
- `dotnet tool restore`: sucesso.
- `dotnet restore ./PocArquitetura.slnx`: sucesso.
- `dotnet build ./PocArquitetura.slnx --configuration Release --no-restore`:
  sucesso, 0 warnings, 0 errors.
- `dotnet test ./tests/Architecture.Tests/Architecture.Tests.csproj --configuration Release --no-build`:
  sucesso, 67 testes aprovados.
- `git diff --check`: sucesso. Avisos de normalizacao LF/CRLF nos arquivos
  `.c4`, sem erro de whitespace.

`actionlint` nao foi executado porque nenhum workflow foi alterado.

## Riscos Residuais

- A distincao entre runtime e build ainda depende de disciplina editorial ao adicionar novas views.
- Ledger/Balance/Identity mantem estilo historico com camadas internas; nao foram remodelados para evitar churn maior.
- Automacoes semanticas mais fortes podem gerar falsos positivos se tentarem inferir implementacao por nomes de arquivos.
