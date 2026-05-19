# AGENTS.md

## Objetivo

Este repositorio e uma POC de microservicos em .NET com Clean Architecture, DDD, PostgreSQL, Kafka, Outbox, JWT/JWKS, observabilidade e testes automatizados. O trabalho do Codex deve ser pequeno, correto, reproduzivel e coerente com a arquitetura existente.

Responda em portugues, salvo pedido explicito em outro idioma.

## Fontes de verdade

Consulte estes arquivos quando forem relevantes para a tarefa:

1. `README.md`
2. `docs/README.md`
3. `docs/adrs/`
4. `docs/architecture/`
5. `Directory.Packages.props`
6. `Directory.Build.props`
7. `.editorconfig`
8. `global.json`
9. `coverlet.runsettings`
10. `LedgerService.slnx`

## Escopo principal

A solution principal e `LedgerService.slnx`.

Componentes principais:

- `src/Auth.Api`
- `src/LedgerService.Api`
- `src/LedgerService.Application`
- `src/LedgerService.Domain`
- `src/LedgerService.Infrastructure`
- `src/BalanceService.Api`
- `src/BalanceService.Application`
- `src/BalanceService.Domain`
- `src/BalanceService.Infrastructure`
- `tests/*`

## Regras obrigatorias

- Faca a menor mudanca possivel para resolver o problema.
- Preserve as fronteiras entre `Api`, `Application`, `Domain` e `Infrastructure`.
- Nao mova regra de negocio para controller, endpoint, middleware ou infraestrutura.
- Nao coloque detalhes de infraestrutura no `Domain`.
- Nao adicione `Version=` em `PackageReference`; o repositorio usa Central Package Management.
- Nao altere migrations existentes sem necessidade explicita.
- Nao introduza segredos no repositorio.
- Nao invente URLs, portas, contratos, comandos ou arquitetura.
- Quando mudar contrato, fluxo arquitetural, setup local ou comportamento relevante, atualize a documentacao correspondente.
- Nao altere testes apenas para faze-los passar.
- Nao faca push.
- Nao crie branch sem solicitacao explicita.

## Arquitetura

- `Api`: entrada/saida HTTP, autenticacao, autorizacao, Swagger, health/readiness, middlewares e DI.
- `Application`: casos de uso, handlers, services, validacao de entrada e orquestracao.
- `Domain`: entidades, invariantes e modelos de dominio sem dependencia de infraestrutura.
- `Infrastructure`: EF Core, PostgreSQL, Kafka, Outbox, DLQ, hosted services, integracoes e detalhes tecnicos.

Ao alterar endpoints protegidos, revise issuer, audience, scopes, policies e autorizacao por merchant. Ao alterar eventos, preserve correlacao, headers, idempotencia e contrato entre produtor e consumidor.

Quando alterar endpoints, contratos HTTP, payloads, status codes, autenticacao, autorizacao ou headers usados por cenarios de carga, avalie se os testes k6 precisam ser atualizados. Execute testes de carga apenas quando houver pedido explicito ou mudanca diretamente relacionada aos cenarios.

## EF Core

Sempre que alterar entidades persistidas, mappings, `DbContext`, indices, constraints, relacionamentos ou tipos de coluna, avalie se a mudanca exige migration. Se alterar schema, crie nova migration. Nao modifique migrations antigas apenas para organizar.

Mudancas de persistencia com impacto estrutural, transacional, relacional ou comportamental devem avaliar atualizacao de documentacao e ADR.

## Documentacao

- Atualize `README.md` apenas como porta de entrada.
- Mantenha detalhes em `docs/`.
- Atualize `docs/README.md` quando adicionar, remover ou consolidar documentos.
- Atualize `docs/architecture/` e LikeC4 quando mudar componente, relacao arquitetural, servico, banco, fila, topico, observabilidade ou integracao relevante.
- Crie ou atualize ADR quando houver decisao arquitetural, contrato entre servicos, estrategia de persistencia, mensageria, observabilidade, seguranca, resiliencia, integracao externa, estrutura de projeto ou mudanca relevante de comportamento.
- Nao reescreva ADR historica como se fosse documentacao atual; preserve a decisao original.

## Skills do Codex

Use skills em `.agents/skills/` quando o pedido combinar com o `description` da skill.

Roteamento atual:

- `dotnet-service-change`: mudancas funcionais nos servicos .NET.
- `integration-tests-dotnet`: testes de integracao .NET ou estrategia especifica de integracao.
- `ci-release-governance`: GitHub Actions, GitVersion, releases, coverage, hooks e automacoes.
- `repository-governance-sdd`: `AGENTS.md`, skills, ADRs, prompts, documentacao de processo e governanca.

Em caso de conflito entre este arquivo e uma skill, preserve este arquivo como orientacao global do repositorio.

## Comandos baseline

```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Para cobertura com gate:

```powershell
./test.ps1
```

No Linux/macOS:

```bash
./test.sh
```

Testes com Testcontainers/PostgreSQL precisam acessar uma Docker-compatible API. No Windows, se `DOCKER_HOST=npipe:////./pipe/docker_engine` quebrar Docker.DotNet, normalize apenas no processo do teste:

```powershell
$env:DOCKER_HOST='npipe://./pipe/docker_engine'
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

## Commits

Sempre que houver alteracao em arquivos do repositorio, crie commit semantico ao final da tarefa, salvo instrucao explicita para nao commitar.

Use Conventional Commits:

- `feat:`
- `fix:`
- `refactor:`
- `test:`
- `docs:`
- `chore:`
- `ci:`

Antes de commitar, revise o diff e execute validacoes proporcionais ao impacto. Nao crie commit se houver falha de build/teste sem registrar claramente o motivo.
