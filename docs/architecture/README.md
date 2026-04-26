# Documentacao arquitetural

Esta pasta registra a leitura arquitetural atual da POC e o modelo LikeC4 usado para visualizar o sistema.

Arquivos principais:

- `model.c4`: modelo estrutural do ecossistema, containers e componentes reais.
- `views.c4`: views LikeC4 para landscape, containers e componentes por servico.
- `boundaries.md`: regras de fronteira entre camadas, responsabilidades e anti-patterns.
- `decisions.md`: avaliacao critica, riscos e roadmap pragmatico de evolucao.

Classificacao atual: arquitetura hibrida, com predominancia de Clean Architecture/DDD em LedgerService e BalanceService, elementos hexagonais por portas de persistencia/mensageria, camada HTTP tradicional e CQRS/projecao assincrona entre escrita e leitura.

