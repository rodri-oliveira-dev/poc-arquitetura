# ADR-0047: Plano futuro de adocao incremental de Testcontainers

## Status
Proposto

## Data
2026-05-06

## Contexto
O repositorio e uma POC de microservicos em .NET 10 com Clean Architecture, DDD, PostgreSQL, Kafka, Outbox, autenticacao JWT via JWKS, testes automatizados e pipelines GitHub Actions.

A solucao principal e `LedgerService.slnx` e contem testes unitarios e testes de integracao para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`.

O levantamento dos testes atuais identificou:

- uso de xUnit como framework de testes;
- uso de `WebApplicationFactory` nos projetos de integracao;
- uso de EF Core InMemory nos testes de integracao de Ledger e Balance;
- Kafka desligado nos testes de integracao por `Kafka:Enabled=false`;
- remocao de hosted services nas factories de integracao;
- uso de mocks e fakes em testes unitarios de Application, seguranca, options e processamento Kafka;
- ausencia de WireMock e SQLite no escopo analisado;
- migrations reais em `LedgerService.Infrastructure` e `BalanceService.Infrastructure`;
- PostgreSQL como banco oficial por servico, conforme ADR-0007;
- Outbox com SQL especifico de PostgreSQL, incluindo `FOR UPDATE SKIP LOCKED`;
- `Testcontainers` e `Testcontainers.PostgreSql` ja versionados em `Directory.Packages.props` e referenciados nos projetos de integracao de Ledger e Balance, mas sem uso efetivo encontrado;
- CI em GitHub Actions executando `dotnet test` da solution inteira, com timeout de 20 minutos e gate de cobertura consolidada de 80%.

Os testes atuais sao uteis e relativamente leves, mas parte dos testes chamados de integracao nao valida o provider real de banco, migrations, constraints, transacoes, comportamento de SQL relacional nem locking do PostgreSQL. Essa diferenca e relevante porque a arquitetura adotada depende explicitamente de PostgreSQL, EF Core, transacoes e Outbox.

Ao mesmo tempo, nem todo teste deve usar dependencia real. Testes unitarios, testes de dominio, validadores, mappers, policies, configuracao, Swagger e processamento isolado de mensagens continuam se beneficiando de mocks, fakes ou execucao em memoria.

## Decisao
Manter a adocao de Testcontainers como ajuste futuro, recomendado parcialmente e condicionado a uma migracao incremental, reversivel e mensuravel.

Testcontainers nao deve substituir toda a estrategia de testes do repositorio. A direcao proposta e usar containers apenas onde a fidelidade da dependencia real aumenta a confianca de forma clara.

O primeiro alvo futuro deve ser PostgreSQL, nao Kafka.

O primeiro spike deve:

- criar uma prova de conceito minima com `Testcontainers.PostgreSql`;
- integrar o container PostgreSQL a `WebApplicationFactory`;
- aplicar migrations explicitamente no container, preservando a decisao de nao aplicar migrations no startup das APIs;
- validar um fluxo real do `LedgerService.Api` que grave `ledger_entries`, `idempotency_records` e `outbox_messages`;
- validar um cenario tecnico do Outbox que exercite o caminho relacional de `OutboxMessageRepository`;
- medir tempo local e impacto potencial no CI;
- manter os testes unitarios e testes HTTP leves existentes sem migracao em massa.

Somente apos esse spike deve ser avaliado:

- migrar cenarios selecionados de `BalanceService.IntegrationTests` para PostgreSQL real;
- separar testes com containers por trait/categoria ou workflow dedicado;
- adicionar Kafka via Testcontainers para smoke tests de fluxo Ledger -> Kafka -> Balance;
- tornar uma Docker-compatible API pre-requisito oficial para alguma parte da validacao de pull requests.

Kafka com Testcontainers deve ser tratado como segunda etapa, pois aumenta custo de setup, tempo de execucao e risco de flakiness. O ganho inicial mais claro esta no PostgreSQL, especialmente em migrations, transacoes, constraints, queries e Outbox.

Mocks, fakes, EF InMemory e testes de unidade continuam validos quando o objetivo do teste for comportamento de dominio, aplicacao, contrato HTTP leve ou isolamento de dependencias externas.

## Consequencias

### Beneficios
- Aumenta a confianca nos fluxos que dependem de PostgreSQL real.
- Permite validar migrations em execucao de teste.
- Exercita SQL, constraints, transacoes, provider Npgsql e comportamento relacional.
- Permite cobrir o caminho de Outbox que usa `FOR UPDATE SKIP LOCKED`.
- Reduz risco de divergencia entre EF InMemory e producao/local compose.
- Preserva testes unitarios rapidos e focados.
- Permite migracao gradual, com medicao de tempo e custo antes de ampliar escopo.
- Aproveita dependencias Testcontainers ja centralizadas no repositorio.

### Trade-offs / custos
- Exige Docker-compatible API no ambiente local e no CI, se os testes forem obrigatorios.
- Aumenta o tempo de execucao da suite quando containers entram no fluxo padrao.
- Exige desenho de fixtures, ciclo de vida assincrono e limpeza de dados.
- Pode aumentar flakiness se readiness, timeouts e isolamento nao forem tratados.
- Exige decisao sobre paralelizacao, banco por classe, schema por teste, transacao por teste ou limpeza explicita.
- Pode exigir separacao entre testes unitarios, integracao leve e integracao com infraestrutura real.
- Aumenta a complexidade de debugging por envolver logs de container e portas dinamicas.

### Riscos
- Tornar todos os testes mais lentos ao migrar cenarios sem ganho real.
- Introduzir dependencia obrigatoria de runtime Docker-compatible em todo PR sem avaliar custo no pipeline.
- Criar testes frageis por compartilhamento indevido de estado entre classes.
- Mascarar problemas de design ao transformar testes unitarios em testes de infraestrutura.
- Subir Kafka cedo demais e aumentar custo antes de estabilizar PostgreSQL.
- Criar drift entre testes com containers, `compose.yaml`, documentacao local e pipelines.

## Alternativas consideradas

1. **Manter somente EF InMemory**
   - Mantem velocidade e simplicidade.
   - Rejeitado como estrategia unica porque nao valida PostgreSQL, migrations, SQL especifico, constraints, transacoes nem locks usados pelo Outbox.

2. **Migrar todos os testes de integracao para Testcontainers**
   - Aumenta fidelidade de forma ampla.
   - Rejeitado para a fase inicial porque muitos testes atuais validam contrato HTTP, autenticacao, autorizacao e limites operacionais sem precisar de banco real.

3. **Usar SQLite para testes relacionais**
   - Poderia validar parte do comportamento relacional com menor custo.
   - Rejeitado como alvo principal porque o projeto depende de PostgreSQL e ja usa SQL especifico desse provider.

4. **Subir o `compose.yaml` completo para testes**
   - Validaria a topologia local inteira.
   - Rejeitado como primeiro passo porque aumenta custo, acoplamento e tempo de CI. Testcontainers deve subir a menor dependencia necessaria para o teste.

5. **Usar banco PostgreSQL compartilhado no CI**
   - Evita subir container por execucao de teste.
   - Rejeitado porque reduz isolamento, dificulta reproducibilidade e aumenta risco de interferencia entre execucoes.

6. **Adotar Testcontainers apenas para PostgreSQL no primeiro momento**
   - Alternativa recomendada para o spike. Oferece maior ganho de fidelidade com menor custo inicial.

## Plano futuro proposto

### Fase 1: Preparacao
- Confirmar Docker-compatible API local e no GitHub Actions.
- Medir baseline atual de `dotnet test ./LedgerService.slnx --configuration Release`.
- Escolher um conjunto pequeno de testes candidatos.
- Definir criterio de sucesso: testes passam local e no CI, tempo adicional aceitavel e sem reducao de cobertura.
- Garantir que a suite atual continua passando antes da mudanca.

### Fase 2: Spike tecnico
- Criar fixture minima de PostgreSQL com Testcontainers.
- Integrar a fixture a `LedgerApiFactory`.
- Aplicar migrations de `AppDbContext` explicitamente.
- Validar criacao de lancamento com persistencia real.
- Validar idempotencia com banco real.
- Criar ou adaptar teste tecnico do Outbox para exercitar SQL relacional.
- Medir tempo de execucao.

### Fase 3: Estrutura de testes
- Definir ciclo de vida compartilhado por collection ou classe.
- Evitar container por teste se isso tornar a suite lenta.
- Definir isolamento por limpeza de dados, banco por classe, schema por teste ou transacao por teste.
- Documentar trade-offs.

### Fase 4: Migracao incremental
- Migrar somente cenarios com ganho claro de fidelidade.
- Manter testes unitarios como unitarios.
- Manter mocks e fakes onde a dependencia externa nao precisa ser exercitada.
- Substituir InMemory apenas quando houver diferenca relevante de provider, SQL, transacao, migrations, locks ou constraints.
- Evitar refatoracoes amplas junto com a migracao.

### Fase 5: CI/CD
- Validar suporte a Docker-compatible API no pipeline.
- Avaliar impacto no timeout atual de 20 minutos.
- Separar testes com containers por trait/categoria se necessario.
- Preservar coleta de cobertura com `coverlet.runsettings`.
- Permitir execucao seletiva de testes com containers.

### Fase 6: Documentacao
- Documentar como rodar os testes com containers localmente.
- Documentar pre-requisitos e troubleshooting.
- Documentar estrategia de isolamento de dados.
- Atualizar esta ADR ou criar nova ADR se Testcontainers passar a ser requisito oficial do pipeline.

## Testes candidatos para o primeiro spike
- `tests/LedgerService.IntegrationTests/Tests/LancamentosAuthorizationTests.cs`
  - fluxo de criacao de lancamento com `ledger.write`;
  - replay por idempotencia com a mesma `Idempotency-Key`.
- `tests/LedgerService.IntegrationTests/Tests/HealthEndpointTests.cs`
  - readiness com banco PostgreSQL real e Kafka desabilitado.
- `tests/LedgerService.Tests/OutboxMessageRepositoryTests.cs`
  - novo cenario ou adaptacao especifica para `ClaimPendingAsync` usando PostgreSQL real.
- Em etapa posterior, `tests/BalanceService.IntegrationTests/Tests/ConsolidadosEndpointsTests.cs`
  - leitura diaria e por periodo com dados persistidos em PostgreSQL real.

## Proximos passos
- Confirmar runtime Docker-compatible local e no GitHub Actions.
- Medir baseline atual da suite.
- Criar branch separada para spike, se aprovado.
- Implementar fixture minima de PostgreSQL em Testcontainers apenas para Ledger.
- Aplicar migrations no container de teste.
- Medir tempo e estabilidade.
- Decidir se testes com containers entram no fluxo padrao ou em execucao seletiva.
- Avaliar nova ADR ou atualizacao desta antes de tornar Docker-compatible API requisito oficial de PR.
