# ADR-0034: Definicao de Boundaries Arquiteturais e Estrutura de Camadas

## Status
Aceito

## Data
2026-04-26

## Contexto
O repositorio e uma POC de microservicos .NET com LedgerService, BalanceService e Auth.Api. Ja existem ADRs aceitas para Clean Architecture/DDD por servico, Kafka com Outbox, banco por microservico, JWT/JWKS, observabilidade, autorizacao por merchant, DLQ e governanca documental.

A estrutura real da solucao mostra:

- LedgerService com projetos `Api`, `Application`, `Domain` e `Infrastructure`;
- BalanceService com projetos `Api`, `Application`, `Domain` e `Infrastructure`;
- Auth.Api em projeto unico;
- Kafka, Outbox, DLQ, PostgreSQL, JWKS/JWT, health/readiness, Swagger, OpenTelemetry e background processing;
- integracao assincrona entre Ledger e Balance via `LedgerEntryCreated.v1`.

## Problema atual
O projeto declara Clean Architecture/DDD, mas precisa de uma leitura pragmatica para evitar dois riscos opostos:

- aplicar camadas, interfaces e patterns por reflexo, criando overengineering para uma POC;
- simplificar demais e permitir acoplamento indevido entre HTTP, dominio, persistencia, mensageria e seguranca.

Tambem ha assimetrias reais entre servicos, como repositories no Domain do Ledger e em Application do Balance, MediatR apenas no Balance, Outbox como entidade de Domain no Ledger e Auth.Api deliberadamente sem camadas internas.

## Analise encontrada
A arquitetura atual e hibrida:

- predominantemente Clean Architecture/DDD em LedgerService e BalanceService;
- parcialmente hexagonal onde ha portas de persistencia/mensageria e implementacoes em Infrastructure;
- layered architecture na camada HTTP;
- CQRS pragmatico entre Ledger e Balance, com escrita e leitura separadas por evento/projecao.

LedgerService justifica camadas internas por ter idempotencia, transacao, dominio com invariantes, persistencia e Outbox.

BalanceService justifica camadas internas por ter consultas, consumidor Kafka, DLQ, idempotencia de eventos e projecao.

Auth.Api nao justifica camadas adicionais neste momento. Ele e uma API de autenticacao de POC com emissao JWT, JWKS, chave RSA e hardening basico. Dividi-lo em `Auth.Application`, `Auth.Domain` e `Auth.Infrastructure` agora aumentaria custo sem ganho proporcional.

## Decisao
Manter a arquitetura como minimalista e pragmatica, com robustez seletiva:

- LedgerService e BalanceService permanecem com `Api`, `Application`, `Domain` e `Infrastructure`.
- Auth.Api permanece em projeto unico enquanto continuar sendo autenticacao de POC.
- Boundaries serao documentados em `docs/architecture/boundaries.md`.
- Diagramas LikeC4 serao mantidos em `docs/architecture/model.c4` e `docs/architecture/views.c4`.
- A avaliacao critica, riscos e roadmap ficam em `docs/architecture/decisions.md`.
- Novas camadas, interfaces ou frameworks exigem motivacao concreta, nao simetria visual.

Regras principais:

- `Api` orquestra HTTP, auth, middlewares, Swagger, health/readiness e DI.
- `Application` contem casos de uso, handlers, validacao de aplicacao, transacao e idempotencia.
- `Domain` contem invariantes e comportamento sem depender de ASP.NET, EF Core ou Kafka.
- `Infrastructure` contem EF Core, migrations, repositories concretos, Kafka, DLQ, Outbox publisher e hosted services.

## Por que nao usar mais camadas
Mais camadas agora criariam custo sem evidencia suficiente:

- Auth.Api ficaria artificialmente complexo.
- LedgerService e BalanceService ja possuem separacao suficiente para o tamanho atual.
- Camadas adicionais para contratos, workers ou observabilidade poderiam duplicar abstracoes antes de haver multiplas implementacoes.
- Shared kernel ou bibliotecas comuns poderiam acoplar servicos prematuramente.

## Por que nao usar menos camadas
Menos camadas tambem aumentaria risco:

- LedgerService mistura HTTP, idempotencia, dominio, EF Core e Outbox se virar projeto unico.
- BalanceService mistura consulta HTTP, consumer Kafka, DLQ, idempotencia e projecao.
- Regras de dominio ficariam mais vulneraveis a dependencias de infraestrutura.
- Testes unitarios e de integracao ficariam menos focados.

## Consequencias

### Beneficios
- Mantem disciplina arquitetural onde ha complexidade real.
- Evita transformar a POC em uma arquitetura cerimonial.
- Deixa explicito que Auth.Api e excecao pragmatica, nao inconsistencia acidental.
- Ajuda revisores a identificar vazamentos entre camadas.
- Cria base visual em LikeC4 para discussao tecnica do time.

### Trade-offs / custos
- As assimetrias atuais continuam existindo ate um refactor dedicado.
- O time precisa manter documentacao e diagramas sincronizados com mudancas relevantes.
- Algumas decisoes continuam pragmaticas, como Outbox no Domain do Ledger e default de currency no Balance.
- A arquitetura depende de revisao disciplinada, nao apenas de barreiras fisicas de projeto.

### Consequencias negativas
- Pode haver interpretacao diferente sobre onde colocar novas portas enquanto nao houver padrao unico entre Ledger e Balance.
- Manter Auth.Api simples exige cuidado para nao deixar regra de identidade real crescer dentro de endpoints.
- LikeC4 adiciona um artefato novo que precisa ser mantido.

## Alternativas consideradas

1. **Aplicar quatro camadas em todos os servicos, inclusive Auth.Api**
   - Pros: simetria visual.
   - Contras: overengineering e custo sem ganho real no Auth atual.

2. **Unificar cada microservico em um unico projeto**
   - Pros: menos arquivos e menos DI.
   - Contras: acoplamento maior em Ledger e Balance, perda de boundaries e testes menos focados.

3. **Migrar para arquitetura vertical slice em todos os servicos**
   - Pros: organizacao por fluxo e menor dependencia de camadas horizontais.
   - Contras: refactor estrutural prematuro, pouco beneficio imediato e risco de quebrar uma POC que ja esta coerente.

4. **Criar shared kernel/contratos compartilhados**
   - Pros: reduz duplicacao de modelos de evento.
   - Contras: acopla servicos antes de resolver governanca de versionamento e compatibilidade.

## Proximos passos
- Usar `docs/architecture/` como referencia em revisoes arquiteturais.
- Criar testes de contrato para `LedgerEntryCreated.v1`.
- Tratar a ausencia de currency no evento como divida arquitetural explicita.
- Avaliar clock abstrato no LedgerService em refactor futuro.
- Reavaliar Auth.Api se a POC evoluir para identidade real ou se ADR de Keycloak for implementada.

