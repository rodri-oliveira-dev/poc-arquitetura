# Revisao C4/LikeC4 - design

## Estrategia

A revisao usa o codigo e a configuracao versionada como fonte de verdade. ADRs, specs antigas, roadmap e relatorios entram como contexto historico, mas nao substituem evidencias em `src/`, `tests/`, Compose, contratos e scripts.

## Inventario sintetico

| Artefato | Papel | Situacao | Acao |
| --- | --- | --- | --- |
| `docs/architecture/model.c4` | Modelo central, elementos e relacoes | Abrange os seis contexts, runtime local, mensageria, externos e observabilidade | Manter com revisao de evidencias |
| `docs/architecture/views.c4` | Views C4/dynamic | Contem contexto, containers, componentes, fluxos e deployment | Manter e reforcar navegacao textual |
| `docs/architecture/deployment.c4` | Deployment local Compose | Mapeia services locais para containers logicos | Manter |
| `docs/architecture/README.md` | Guia de leitura arquitetural | Rico, mas podia explicitar jornadas por perfil | Expandir |
| `docs/README.md` | Indice documental | Precisa listar a nova spec | Atualizar |
| `docs/maturity.md` | Estado resumido de maturidade | Drift sobre AuditService.Worker/Kafka | Corrigir |
| `README.md` | Entrada do repo com Mermaid resumido | Coerente com LikeC4 atual; Mermaid e apenas resumo editorial | Manter |
| `docs/specs/payment-stripe/integration-flows.md` | Mermaid historico de spec | Historico de implementacao, nao fonte canonica atual | Manter como spec historica |

## Niveis C4

- System Context: `systemLandscape` mostra ator, bounded contexts, Keycloak, Stripe, Kafka, e-mail, Nginx e observabilidade sem controllers/classes.
- Container: `businessContainers`, `integrationContainers`, `platformContainers` e `containers` separam leitura por pergunta e mostram executaveis, schemas e recursos.
- Component: views por API/Worker mostram camadas, composition roots, processors, ports, adapters e persistencia quando arquiteturalmente relevantes.
- Dynamic: fluxos principais mostram sequencia temporal, idempotencia, Outbox, Inbox, chamadas HTTP, Kafka e DLQ.
- Deployment: `localDeployment` mostra Docker Compose local; GCP permanece baseline futuro em documentacao textual, nao deployment atual.

## Design da experiencia

1. Primeira leitura: contexto e containers de negocio.
2. Aprofundamento por integracao: Kafka, Keycloak, Stripe, e-mail e HTTP service-to-service.
3. Aprofundamento por bounded context: component views de API/Worker.
4. Aprofundamento por fluxo: dynamic views curtas e nomeadas por objetivo.
5. Operacao: platform/runtime, deployment local, observabilidade, DLQ/replay e runbooks.

## Decisoes

- Nao criar uma nova fonte de metadados agora. O modelo LikeC4 ja compila e os testes arquiteturais ja cobrem regras de projeto; acoplamento automatico adicional teria custo maior que o beneficio nesta revisao documental.
- Nao versionar artefatos estaticos gerados. O workflow e `npm run architecture:build` continuam como fonte reprodutivel.
- Manter Mermaid do README como resumo editorial de entrada, porque ele nao tenta substituir o LikeC4 e esta alinhado aos fluxos principais.
- Corrigir apenas drift textual confirmado em documento atual (`docs/maturity.md`), preservando specs antigas como historico.
