# Revisao C4/LikeC4 - report

## 1. Resumo executivo

Situacao inicial: a documentacao LikeC4 ja estava substancialmente alinhada ao codigo atual, com `model.c4`, `views.c4` e `deployment.c4` cobrindo os seis bounded contexts, Kafka default, Pub/Sub legado/opcional, PaymentService, AuditService, observabilidade e deployment local.

Estrategia adotada: validar estaticamente o modelo contra projetos, workers, DbContexts, adapters, testes, Compose e documentacao operacional; corrigir drift textual confirmado; registrar a revisao em SDD sem reescrever diagramas que ja estavam coerentes.

Principais divergencias encontradas: `docs/maturity.md` ainda afirmava que o AuditService nao possuia worker/Kafka, mas a implementacao atual possui `AuditService.Worker`, consumer Kafka de `AuditRecordRequested.v1`, DLQ e testes.

Impacto das correcoes: a documentacao textual passa a refletir melhor o estado real de AuditService, a navegacao arquitetural fica mais explicita para diferentes publicos e `systemLandscape` foi simplificada para retirar telemetria do primeiro mapa.

## 2. Inventario

Arquivos LikeC4 encontrados:

| Arquivo | Conteudo | Situacao | Acao |
| --- | --- | --- | --- |
| `docs/architecture/model.c4` | Modelo, elementos, relacionamentos e tags | Alinhado ao runtime atual | Manter |
| `docs/architecture/views.c4` | Views de contexto, containers, componentes, dynamic e deployment | Alinhado e legivel por recortes; `systemLandscape` foi simplificada | Corrigir |
| `docs/architecture/deployment.c4` | Deployment local Docker Compose | Alinhado a services locais | Manter |

Views encontradas: `systemLandscape`, `containers`, `businessContainers`, `integrationContainers`, `platformContainers`, `ledgerBalanceProjectionFlow`, `identityRegistrationFlow`, `kafkaFlow`, `observabilityFlow`, `identityServiceComponents`, `localDeployment`, `ledgerApiComponents`, `ledgerWorkerComponents`, `pubSubLegacyProjectionFlow`, `balanceApiComponents`, `balanceWorkerComponents`, `transferApiComponents`, `transferWorkerComponents`, `transferSagaSuccessFlow`, `transferSagaCompensationFlow`, `identityComponents`, `paymentCreateFlow`, `paymentWebhookInboxFlow`, `paymentLedgerMaterializationFlow`, `paymentRefundFlow`, `paymentApiComponents`, `paymentWorkerComponents`, `auditApiComponents`, `auditWorkerComponents` e `auditKafkaIngestionFlow`.

Diagramas estaticos relacionados: Mermaid resumido no `README.md` e Mermaid historico em specs, especialmente `docs/specs/payment-stripe/integration-flows.md` e `docs/specs/documentation-experience-review/design.md`. Eles foram classificados como resumo editorial ou historico SDD, nao fonte canonica.

Documentos textuais relacionados: `docs/architecture/README.md`, `docs/architecture/boundaries.md`, `docs/architecture/decisions.md`, `docs/architecture/production-readiness.md`, `docs/architecture/payment-service.md`, `docs/architecture/audit-service.md`, `docs/events/README.md`, `docs/operations/*`, `docs/development/github-pages.md`, `docs/development/tooling.md`, `README.md`, `docs/README.md` e ADRs relevantes.

## 3. Divergencias entre modelo e implementacao

| Elemento ou relacao | Documentacao anterior | Implementacao real | Evidencia | Correcao |
| --- | --- | --- | --- | --- |
| AuditService.Worker | `docs/maturity.md` dizia que ainda nao havia worker ou Kafka | Existe `AuditService.Worker` com consumer Kafka opcional, DLQ e testes | `src/audit/AuditService.Worker`, `tests/audit/AuditService.Worker.Tests`, `docs/operations/audit-worker.md` | Corrigido em `docs/maturity.md` |
| `systemLandscape` | Incluia observabilidade e suas relacoes de telemetria no primeiro mapa | Observabilidade e assunto operacional detalhado em `platformContainers` e `observabilityFlow` | PNG exportado indicou excesso visual; views operacionais ja existiam | Removido `include observability` de `systemLandscape` |
| Producers de auditoria | Modelo declara ausencia de producers reais | Confirmado: contrato e consumer existem, produtores nos demais dominios ainda nao | `docs/events/README.md`, `docs/development/audit-api.md`, codigo em `src/audit` | Mantido no LikeC4 como estado atual |
| PaymentService -> Ledger | Modelo mostra chamada HTTP do Worker para Ledger | Confirmado: worker materializa credito/estorno via porta `ILedgerEntryGateway` | `src/payment`, `tests/payment`, `docs/operations/payment-worker.md` | Mantido |
| Shared | Modelo nao trata Shared como runtime service | Confirmado: Shared sao bibliotecas/projetos referenciados | `src/Shared/*` | Mantido |

## 4. Bounded contexts

| Contexto | Representacao final | APIs | Workers | Persistencia | Integracoes | Risco residual |
| --- | --- | --- | --- | --- | --- | --- |
| LedgerService | Fonte de fatos financeiros e Outbox | `LedgerService.Api` | `LedgerService.Worker` | schema `ledger` | Kafka default, Pub/Sub legado, JWT/JWKS | Validacao visual manual depende da UI gerada |
| BalanceService | Projecao de saldo | `BalanceService.Api` | `BalanceService.Worker` | schema `balance` | Kafka default, Pub/Sub legado, DLQ | Rebuild/replay ficam em docs operacionais |
| TransferService | Saga orquestrada | `TransferService.Api` | `TransferService.Worker` | schema `transfer` | HTTP para Ledger, Kafka-only para eventos da Saga | Confirmacao `Compensated` permanece evolucao futura |
| PaymentService | Pagamentos externos e materializacao Ledger | `PaymentService.Api` | `PaymentService.Worker` | schema `payment` | Stripe/fake provider, webhook, HTTP para Ledger | Stripe real depende de secrets externos |
| IdentityService | Cadastro e vinculo com IdP | `IdentityService.Api` | Nao possui Worker | schema `identity` | Keycloak Admin API, Mailpit/Resend | Side effects de e-mail nao usam Outbox |
| AuditService | Auditoria HTTP e consumer canonico | `AuditService.Api` | `AuditService.Worker` | schema `audit` | Kafka consumer `AuditRecordRequested.v1` | Sem producers reais nos demais dominios |

## 5. Fluxos

Fluxos revisados e mantidos: lancamento financeiro e saldo, cadastro Identity/Keycloak, Kafka default, Pub/Sub legado, transferencia feliz, transferencia com compensacao, criacao de Payment, webhook/Inbox, Payment -> Ledger, refund, observabilidade e auditoria Kafka.

Correcoes de sequencia/direcao: nenhuma correcao nos arquivos `.c4` foi necessaria; as setas principais ja apontavam o iniciador real da comunicacao, incluindo Transfer/Payment chamando Ledger por HTTP e Stripe chamando apenas a API de Payment.

## 6. Niveis C4

- Contexto: `systemLandscape`.
- Containers: `businessContainers`, `integrationContainers`, `platformContainers`, `containers`.
- Componentes: views por API e Worker dos contexts implementados.
- Deployment: `localDeployment`.
- Dynamic views: fluxos de Ledger/Balance, Identity, Transfer, Payment, Pub/Sub legado e Audit.
- Views removidas ou divididas: nenhuma; a decomposicao existente ja evita um unico diagrama gigante.

## 7. Experiencia visual

O modelo ja usa tags semanticas (`api`, `worker`, `database`, `broker`, `topic`, `external`, `identity-provider`, `observability`, `optional`, `future`, `legacy`) e um `styleGroup architectureTheme`. A revisao manteve essa taxonomia, reforcou a documentacao textual de jornadas e simplificou `systemLandscape` ao retirar a stack de observabilidade do primeiro mapa. A inspecao PNG mostrou que as dynamic views de Payment e Audit ficam legiveis; `businessContainers` segue densa, mas adequada como segunda leitura e complementada por views por contexto.

## 8. Jornada de leitura

- Rapida: `systemLandscape`, `businessContainers`, `ledgerBalanceProjectionFlow`, `transferSagaSuccessFlow`, `paymentLedgerMaterializationFlow`.
- Iniciantes: `docs/architecture/README.md`, `systemLandscape`, `businessContainers`, glossario de niveis C4, depois fluxos principais.
- Desenvolvedores: component view do contexto alterado, contratos em `docs/development/*-api.md`, eventos em `docs/events/README.md`.
- Arquitetos: `integrationContainers`, `kafkaFlow`, `platformContainers`, ADRs e `production-readiness.md`.
- Operacao: `localDeployment`, `observabilityFlow`, runbooks de DLQ/replay, Payment Worker e Audit Worker.

## 9. Validacoes

Comandos planejados/executados nesta revisao:

```powershell
npm ci
npx likec4 validate docs/architecture
npm run architecture:build
npx likec4 export json docs/architecture -o dist/architecture-model.json
npx likec4 export png docs/architecture -o dist/architecture-png --flat -f systemLandscape -f businessContainers -f integrationContainers -f localDeployment -f paymentLedgerMaterializationFlow -f auditKafkaIngestionFlow -f transferSagaSuccessFlow -f ledgerBalanceProjectionFlow --description --notation
dotnet restore .\PocArquitetura.slnx
dotnet build .\PocArquitetura.slnx --configuration Release --no-restore
dotnet test .\tests\Architecture.Tests\Architecture.Tests.csproj --configuration Release --no-build
git diff --check
```

Resultados:

- `npm ci`: aprovado, 0 vulnerabilidades reportadas; npm avisou sobre allow-scripts pendente para `esbuild`.
- `npx likec4 validate docs/architecture`: aprovado, 3 arquivos validos.
- `npm run architecture:build`: aprovado, todas as views layoutadas e site estatico gerado em `dist/architecture`.
- `npx likec4 export json`: aprovado, modelo layoutado exportado para `dist/architecture-model.json`.
- `npx likec4 export png`: primeira tentativa falhou por ausencia do Chromium headless do Playwright; apos `npx playwright install chromium`, exportacao das 8 views principais aprovou.
- Inspecao visual: `systemLandscape` inicial estava carregada demais por telemetria e foi simplificada; Payment/Audit dynamic views estavam legiveis; `businessContainers` e densa, mas aceitavel como segunda leitura.
- `dotnet restore .\PocArquitetura.slnx`: aprovado.
- `dotnet build .\PocArquitetura.slnx --configuration Release --no-restore`: aprovado com 49 warnings preexistentes, 0 erros.
- `dotnet test .\tests\Architecture.Tests\Architecture.Tests.csproj --configuration Release --no-build`: aprovado, 67 testes.
- `git diff --check`: aprovado sem erro de whitespace; Git exibiu aviso esperado de normalizacao LF/CRLF em `docs/architecture/views.c4`.

## 10. Arquivos alterados

Modelos/views/estilos:

- `docs/architecture/views.c4`

Markdown:

- `docs/specs/c4-likec4-architecture-review/requirements.md`
- `docs/specs/c4-likec4-architecture-review/design.md`
- `docs/specs/c4-likec4-architecture-review/tasks.md`
- `docs/specs/c4-likec4-architecture-review/report.md`
- `docs/architecture/README.md`
- `docs/README.md`
- `docs/maturity.md`

Scripts/workflows/artefatos gerados: nenhum.

## 11. Riscos residuais

- A inspecao visual final depende da renderizacao local do LikeC4; o build valida sintaxe e geracao, mas nao substitui revisao humana da UI.
- A ausencia de producers reais para auditoria deve continuar destacada ate a primeira integracao produtiva ser implementada.
- Specs antigas podem conter trechos historicos propositalmente desatualizados; elas nao devem ser usadas como fonte canonica atual.
- Validacoes automaticas estruturais entre LikeC4 e catalogo arquitetural podem ser consideradas futuramente, mas nao foram adicionadas para evitar fragilidade e custo de manutencao nesta revisao.
