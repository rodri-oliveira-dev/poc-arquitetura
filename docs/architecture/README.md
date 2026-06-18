# Documentacao arquitetural

Esta pasta registra a leitura arquitetural atual da POC e o modelo LikeC4 usado para visualizar o sistema.

Arquivos principais:

- `model.c4`: modelo estrutural do ecossistema, containers e componentes reais, distinguindo Kafka default, Pub/Sub emulator local e Pub/Sub real explicito/legado na GCP.
- `deployment.c4`: modelo de deployment local que associa servicos do `compose.yaml` e overlays locais aos elementos logicos com `instanceOf`, alimentando a aba `Deployments` do LikeC4.
- `views.c4`: views LikeC4 para landscape, containers, fluxo Kafka, Pub/Sub real explicito/legado na GCP, observabilidade local e componentes por processo.
- `boundaries.md`: regras de fronteira entre camadas, responsabilidades e anti-patterns.
- `decisions.md`: avaliacao critica, riscos e roadmap pragmatico de evolucao.
- `production-readiness.md`: baseline recomendado para uma evolucao futura em GCP mais proxima de producao, sem declarar prontidao produtiva nem implementar infraestrutura nova.
- [`../README.md`](../README.md): indice geral da documentacao.

Classificacao atual: arquitetura hibrida, com predominancia de Clean Architecture/DDD em LedgerService e BalanceService, TransferService com Saga orquestrada, API HTTP, Worker dedicado e Outbox Kafka explicita, elementos hexagonais por portas de persistencia/mensageria onde ja existem adapters, camada HTTP tradicional, workers dedicados para processamento assincrono e CQRS/projecao assincrona entre escrita e leitura.

## Visualizacao

O site LikeC4 e publicado no GitHub Pages pelo workflow `architecture-pages`:

<https://rodri-oliveira-dev.github.io/poc-arquitetura/>

Para gerar localmente:

```bash
npm ci
npm run architecture:build
```

Detalhes operacionais: [`docs/development/github-pages.md`](../development/github-pages.md).
