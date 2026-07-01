# AGENTS.md

## Objetivo

Este repositorio e uma POC de microservicos em .NET com Clean Architecture, DDD, PostgreSQL, Kafka como provider padrao dos servicos principais, Pub/Sub explicito/legado, Outbox, JWT/JWKS, observabilidade e testes automatizados. O trabalho do Codex deve ser pequeno, correto, reproduzivel e coerente com a arquitetura existente.

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

- `src/ledger/LedgerService.Api`
- `src/ledger/LedgerService.Application`
- `src/ledger/LedgerService.Domain`
- `src/ledger/LedgerService.Infrastructure`
- `src/balance/BalanceService.Api`
- `src/balance/BalanceService.Application`
- `src/balance/BalanceService.Domain`
- `src/balance/BalanceService.Infrastructure`
- `tests/*`

`src/Auth.Api` permanece apenas como legado de autenticacao de POC, fora da stack principal, com testes proprios enquanto o projeto existir.

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
- Quando adicionar endpoint ou alterar contrato HTTP, payload, status code, autenticacao, autorizacao, header, Swagger/OpenAPI ou comportamento exposto por API, gere novamente os contratos OpenAPI com `./scripts/contracts/openapi/generate.sh` ou `./scripts/contracts/openapi/generate.ps1` e versione as alteracoes em `docs/openapi`.
- Nao altere testes apenas para faze-los passar.
- Nao faca push.
- Nao crie branch sem solicitacao explicita.
- Nunca aplique alteracoes diretamente na branch `main`. Para qualquer mudanca em arquivo do repositorio, crie ou use uma branch de trabalho e deixe a `main` apenas como base de comparacao e destino posterior de PR.
- Em refatoracoes, preserve o comportamento observavel existente, salvo quando a tarefa pedir explicitamente uma mudanca funcional.
- Nao misture refatoracao estrutural com mudanca funcional sem explicar claramente o motivo.
- Antes de finalizar alteracoes, verifique se a tarefa deixou arquivos vazios ou contendo apenas whitespace. Remova arquivos vazios sem funcao clara, especialmente classes, testes, documentacao ou configuracoes abandonadas por refatoracao. Preserve apenas placeholders intencionais, como `.gitkeep`, ou arquivos vazios exigidos por ferramenta e com proposito evidente. Nao crie arquivos vazios apenas para reservar estrutura.

## Criterio de implementacao

Nao implemente recomendacoes apenas porque sao boas praticas genericas.

Antes de alterar codigo, infraestrutura, testes ou documentacao, avalie se a mudanca faz sentido para o tipo de componente, ambiente de execucao, custo operacional, complexidade adicionada, beneficio observavel e risco de criar uma solucao sem uso real.

Classifique a recomendacao como:

1. necessaria para corrigir bug, risco ou requisito explicito;
2. util, mas opcional;
3. valida apenas para producao;
4. desnecessaria no contexto atual;
5. potencialmente prejudicial.

Somente implemente quando houver justificativa tecnica clara. Quando a recomendacao nao fizer sentido, registre a decisao tecnica em vez de criar implementacao artificial.

Exemplo: workers nao precisam necessariamente expor `/health` ou `/ready` se nao recebem trafego HTTP e se a saude operacional ja e coberta por validacao de configuracao no startup, falha do processo, logs, metricas e alertas. Caso a plataforma de execucao exija probes HTTP, elas devem ser leves e baseadas em estado interno, evitando consultas pesadas a banco, fila, DLQ ou views de grande volume.

## Arquitetura

- `Api`: entrada/saida HTTP, autenticacao, autorizacao, Swagger, health/readiness, middlewares e DI.
- `Application`: casos de uso, handlers, services, validacao de entrada e orquestracao.
- `Domain`: entidades, invariantes e modelos de dominio sem dependencia de infraestrutura.
- `Infrastructure`: EF Core, PostgreSQL, Kafka, Pub/Sub explicito/legado, Outbox, DLQ, hosted services, integracoes e detalhes tecnicos.

Kafka e o provider padrao de mensageria dos fluxos principais de `LedgerService.Worker`, `BalanceService.Worker` e `TransferService.Worker`. Pub/Sub permanece apenas como alternativa explicita/legada para Ledger/Balance quando `Messaging:Provider=PubSub`; nao use Pub/Sub em novos fluxos padrao sem ADR nova. `TransferService` permanece Kafka-only conforme ADR-0087. `Domain` e `Application` nao devem referenciar Kafka, Pub/Sub, topics, subscriptions, partitions, offsets, commits, ack/nack ou clients de transporte; esses detalhes pertencem a adapters e composition roots de Worker/Infrastructure.

Ao alterar endpoints protegidos, revise issuer, audience, scopes, policies e autorizacao por merchant. Ao alterar eventos, preserve correlacao, headers, idempotencia e contrato entre produtor e consumidor.

Quando alterar endpoints, contratos HTTP, payloads, status codes, autenticacao, autorizacao ou headers usados por cenarios de carga, avalie se os testes k6 precisam ser atualizados. Execute testes de carga apenas quando houver pedido explicito ou mudanca diretamente relacionada aos cenarios.

Depois de qualquer mudanca em endpoints, Swagger/OpenAPI ou contratos HTTP, valide que `docs/openapi` esta sincronizado executando o build, o gerador OpenAPI, `npm run openapi:lint` e `git diff --exit-code -- docs/openapi`. Nao edite manualmente os JSONs quando o gerador conseguir produzi-los.

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

## Organizacao dos scripts

Use os caminhos em subpastas de `scripts/` como padrao em documentacao, automacoes e instrucoes novas. Nao use wrappers antigos diretamente em `scripts/` em CI, `package.json` ou tasks do VS Code, e nao recomende remocao deles sem tarefa explicita. A politica principal fica em `docs/development/scripts.md`.

## Skills do Codex

Use skills em `.agents/skills/` quando o pedido combinar com o `description` da skill.
Antes de executar tarefas especializadas, verifique se ha uma skill aplicavel em `.agents/skills/`.
Use a skill como orientacao complementar ao `AGENTS.md`, preservando as regras especificas deste repositorio.
Quando uma skill externa conflitar com padroes locais, os padroes locais prevalecem.

Roteamento atual:

- `dotnet-service-change`: mudancas funcionais nos servicos .NET.
- `dotnet-refactoring-engineer`: refatoracoes, code review tecnico, melhoria de design, reducao de acoplamento, melhoria de coesao, extracao de responsabilidades, revisao de DI, revisao de endpoints, EF Core, legibilidade, testabilidade, performance e aplicacao cuidadosa de boas praticas de engenharia de software.
- `ddd-implementation-vernon`: implementacao, revisao ou refatoracao orientada a DDD em codigo .NET/C#, incluindo bounded contexts, linguagem ubiqua, aggregates, entidades, value objects, repositories, domain events, application services, anticorruption layer e integracao entre contextos.
- `ddd-modeling-vernon`: sessoes de modelagem DDD, discovery, Event Storming, bounded contexts, context map, aggregate design, domain events, glossary, workflows, tipos de dominio e validacao de cenarios.
- `integration-tests-dotnet`: testes de integracao .NET ou estrategia especifica de integracao.
- `ci-release-governance`: GitHub Actions, GitVersion, releases, coverage, hooks e automacoes.
- `repository-governance-sdd`: `AGENTS.md`, skills, ADRs, prompts, documentacao de processo e governanca.
- `nginx-edge-local`: alteracoes, revisoes e diagnosticos da borda local Nginx em `compose.nginx.yaml` e `infra/nginx/`, incluindo HTTPS local, proxy reverso, headers, limites defensivos, logs, Swagger via subdominios `.localhost` e load balance local do `LedgerService.Api`.
- `terraform-gcp-iac`: criacao, revisao e documentacao de infraestrutura GCP com Terraform, incluindo provider Google, modulos, ambientes, state, validacao, IAM, Cloud Run, Cloud SQL, Artifact Registry e Secret Manager.
- `gcp-cli-auth-governance`: uso seguro de `gcloud`, autenticacao, ADC, service accounts, impersonation, IAM de menor privilegio e descoberta segura de recursos GCP.
- `gcp-cloud-run-deployment`: desenho, revisao e documentacao de deploy de APIs, workers e jobs .NET em Cloud Run.
- `gcp-cloud-sql-postgres`: desenho, revisao e documentacao de Cloud SQL for PostgreSQL, conectividade segura, migrations EF Core, backups, HA, IAM, secrets e custos.
- `configuring-opentelemetry-dotnet`: instrumentacao, troubleshooting e evolucao de traces, metricas e logs OpenTelemetry.
- `optimizing-ef-core-queries`: diagnostico e otimizacao de queries EF Core, N+1, tracking, projecoes e armadilhas de performance.
- `coverage-analysis`: analise de cobertura, CRAP score e hotspots de risco para priorizar testes.
- `test-anti-patterns`: auditoria pragmatica de qualidade de testes, flakiness, asserts fracos, over-mocking e acoplamento.

Use `dotnet-refactoring-engineer` quando a tarefa pedir melhorar codigo existente sem alterar comportamento externo. Exemplos:

- reduzir complexidade de uma classe, handler, service ou endpoint;
- separar responsabilidades;
- melhorar nomes e intencao do codigo;
- remover duplicacao real;
- melhorar testabilidade;
- revisar uso de DI;
- revisar queries EF Core;
- transformar codigo procedural em uma estrutura mais coesa;
- revisar se uma abstracao, interface, pattern ou camada faz sentido;
- preparar codigo para uma mudanca funcional futura.

Use `ddd-implementation-vernon` quando a tarefa envolver codigo de dominio ou aplicacao com regras de negocio, invariantes, aggregates, entidades, value objects, repositories, domain events, integration events, Outbox, fronteiras entre contextos ou decisao sobre o que pertence a `Domain`, `Application` ou `Infrastructure`.

Use `ddd-modeling-vernon` quando a tarefa ainda precisar descobrir ou validar o modelo antes de codificar, especialmente quando houver linguagem ambigua, novo fluxo de negocio, desenho de aggregate, Event Storming, context map, glossary, workflows ou necessidade de criar artefatos em `docs/domain/`.

Quando uma mudanca funcional tambem exigir refatoracao, combine `dotnet-service-change` com `dotnet-refactoring-engineer`, mas mantenha a refatoracao pequena, justificada e proporcional ao objetivo da tarefa.

Quando a mudanca envolver regra de negocio relevante ou modelo de dominio, combine `dotnet-service-change` ou `dotnet-refactoring-engineer` com `ddd-implementation-vernon`. Se o modelo ainda estiver incerto, use `ddd-modeling-vernon` antes de alterar codigo.

Quando a refatoracao impactar testes, combine `dotnet-refactoring-engineer` com `integration-tests-dotnet`.

Quando a refatoracao alterar decisao arquitetural, contrato entre servicos, estrategia de persistencia, mensageria, observabilidade, seguranca, estrutura do projeto ou comportamento relevante, avalie tambem `repository-governance-sdd` para atualizar documentacao ou ADR.

Quando a tarefa envolver infraestrutura GCP versionada, combine `terraform-gcp-iac` com as skills especificas do servico afetado. Para Cloud SQL com mudanca em modelo, mapping, migration ou consulta EF Core, combine tambem com `optimizing-ef-core-queries` ou a skill .NET correspondente. Para Cloud Run com ajuste de pipeline, combine tambem com `ci-release-governance`.

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

* `feat:`
* `fix:`
* `refactor:`
* `test:`
* `docs:`
* `chore:`
* `ci:`

Antes de commitar, revise o diff e execute validacoes proporcionais ao impacto. Nao crie commit se houver falha de build/teste sem registrar claramente o motivo.

Quando houver arquivos `.cs` alterados, valide a formatacao antes do commit usando `dotnet format` restrito aos arquivos C# modificados, para antecipar o hook de `pre-push`:

```powershell
$csFiles = @(
  git diff --name-only --diff-filter=ACMRT HEAD -- '*.cs'
  git ls-files --others --exclude-standard -- '*.cs'
) | Sort-Object -Unique
if ($csFiles) { dotnet format ./LedgerService.slnx --include $csFiles --verify-no-changes }
```

Se a verificacao falhar, corrija a formatacao e repita a validacao antes de criar ou emendar o commit. Evite deixar churn de `dotnet format` em arquivos fora do escopo; quando uma execucao ampla tocar arquivos nao relacionados, restaure o ruido e mantenha apenas as mudancas necessarias.
