# AGENTS.md

## Objetivo

Este repositorio e uma POC de microservicos em .NET com Clean Architecture, DDD, PostgreSQL, Kafka como provider padrao dos servicos principais, Pub/Sub explicito/legado, Outbox, JWT/JWKS, observabilidade e testes automatizados.

O trabalho do Codex deve ser pequeno, correto, reproduzivel e coerente com a arquitetura existente. Responda em portugues, salvo pedido explicito em outro idioma.

## Fontes de verdade

Consulte apenas os arquivos relevantes para a tarefa atual:

1. `README.md`
2. `docs/README.md`
3. `docs/adrs/`
4. `docs/architecture/`
5. `Directory.Packages.props`
6. `Directory.Build.props`
7. `.editorconfig`
8. `global.json`
9. `coverlet.runsettings`
10. `PocArquitetura.slnx`
11. A solution contextual correspondente ao componente alterado

Nao carregue indiscriminadamente toda a documentacao. Localize primeiro o contexto, contrato ou decisao diretamente relacionado ao pedido.

## Estrutura e solutions

Os bounded contexts ficam em `src/<contexto>/` e seus testes em `tests/<contexto>/`.

Contextos atuais:

- `ledger`
- `balance`
- `transfer`
- `identity`
- `audit`

Componentes compartilhados ficam em `src/Shared/` e `tests/Shared/`. `tests/Architecture.Tests` e transversal e deve permanecer na solution agregadora.

Solutions contextuais:

- `LedgerService.slnx`
- `BalanceService.slnx`
- `TransferService.slnx`
- `IdentityService.slnx`
- `AuditService.slnx`
- `PocArquitetura.Shared.slnx`

Use `PocArquitetura.slnx` para alteracoes transversais, cobertura consolidada, workflows gerais e testes arquiteturais. Use uma solution contextual quando a mudanca estiver restrita ao respectivo contexto. Use `PocArquitetura.Shared.slnx` para alteracoes restritas aos projetos Shared e seus testes.

## Regras obrigatorias

- Faca a menor mudanca possivel para resolver o problema.
- Preserve as fronteiras entre `Api`, `Application`, `Domain` e `Infrastructure`.
- Nao mova regra de negocio para controller, endpoint, middleware ou infraestrutura.
- Nao coloque detalhes de infraestrutura no `Domain`.
- Nao adicione `Version=` em `PackageReference`; o repositorio usa Central Package Management.
- Nao altere migrations existentes sem necessidade explicita.
- Nao introduza segredos no repositorio.
- Nao invente URLs, portas, contratos, comandos ou arquitetura.
- Nao altere testes apenas para faze-los passar.
- Quando mudar contrato, fluxo arquitetural, setup local ou comportamento relevante, atualize a documentacao correspondente.
- Quando adicionar endpoint ou alterar contrato HTTP, payload, status code, autenticacao, autorizacao, header, Swagger/OpenAPI ou comportamento exposto por API, gere novamente os contratos com `./scripts/contracts/openapi/generate.sh` ou `./scripts/contracts/openapi/generate.ps1` e versione as alteracoes em `docs/openapi`.
- Em refatoracoes, preserve o comportamento observavel existente, salvo quando a tarefa pedir explicitamente uma mudanca funcional.
- Nao misture refatoracao estrutural com mudanca funcional sem explicar claramente o motivo.
- Antes de finalizar, remova arquivos vazios ou contendo apenas whitespace que tenham sido abandonados pela alteracao. Preserve apenas placeholders intencionais ou arquivos exigidos por ferramentas.

## Criterio de implementacao

Nao implemente recomendacoes apenas porque sao boas praticas genericas.

Antes de alterar codigo, infraestrutura, testes ou documentacao, avalie necessidade, ambiente de execucao, custo operacional, complexidade adicionada, beneficio observavel e risco. Implemente somente quando houver requisito, correcao, reducao de risco ou justificativa tecnica clara. Quando a recomendacao nao fizer sentido no contexto atual, registre a decisao em vez de criar implementacao artificial.

## Arquitetura e contratos

- `Api`: entrada e saida HTTP, autenticacao, autorizacao, Swagger, health/readiness, middlewares e DI.
- `Application`: casos de uso, handlers, services, validacao de entrada e orquestracao.
- `Domain`: entidades, invariantes e modelos de dominio sem dependencia de infraestrutura.
- `Infrastructure`: EF Core, PostgreSQL, Kafka, Pub/Sub explicito/legado, Outbox, DLQ, hosted services, integracoes e detalhes tecnicos.

Kafka e o provider padrao dos fluxos principais de `LedgerService.Worker`, `BalanceService.Worker` e `TransferService.Worker`. Pub/Sub permanece apenas como alternativa explicita/legada para Ledger e Balance quando `Messaging:Provider=PubSub`. Nao use Pub/Sub em novos fluxos padrao sem nova ADR. `TransferService` permanece Kafka-only conforme ADR-0087.

`Domain` e `Application` nao devem referenciar Kafka, Pub/Sub, topics, subscriptions, partitions, offsets, commits, ack/nack ou clients de transporte. Esses detalhes pertencem aos adapters e composition roots de Worker/Infrastructure.

Ao alterar endpoints protegidos, revise issuer, audience, scopes, policies e autorizacao por merchant. Ao alterar eventos, preserve correlacao, headers, idempotencia e contrato entre produtor e consumidor.

Quando alterar contratos HTTP usados por cenarios de carga, avalie se os testes k6 precisam ser atualizados. Execute testes de carga somente quando houver pedido explicito ou mudanca diretamente relacionada ao cenario.

Depois de qualquer mudanca em endpoints, Swagger/OpenAPI ou contratos HTTP, valide que `docs/openapi` esta sincronizado executando o build, o gerador OpenAPI, `npm run openapi:lint` e `git diff --exit-code -- docs/openapi`. Nao edite manualmente os JSONs quando o gerador conseguir produzi-los.

## EF Core

Sempre que alterar entidades persistidas, mappings, `DbContext`, indices, constraints, relacionamentos ou tipos de coluna, avalie se a mudanca exige migration. Se alterar schema, crie nova migration. Nao modifique migrations antigas apenas para organizar.

Mudancas de persistencia com impacto estrutural, transacional, relacional ou comportamental devem avaliar atualizacao de documentacao e ADR.

## Documentacao e scripts

- Atualize `README.md` apenas como porta de entrada.
- Mantenha detalhes em `docs/`.
- Atualize `docs/README.md` quando adicionar, remover ou consolidar documentos.
- Atualize `docs/architecture/` e LikeC4 quando mudar componente, relacao arquitetural, servico, banco, fila, topico, observabilidade ou integracao relevante.
- Crie ou atualize ADR quando houver decisao arquitetural, contrato entre servicos, estrategia de persistencia, mensageria, observabilidade, seguranca, resiliencia, integracao externa, estrutura de projeto ou mudanca relevante de comportamento.
- Nao reescreva ADR historica como se fosse documentacao atual; preserve a decisao original.
- Use os caminhos em subpastas de `scripts/` como padrao em documentacao, automacoes e instrucoes novas. A politica principal fica em `docs/development/scripts.md`.

## Skills do Codex

Antes de executar uma tarefa especializada, verifique as skills disponiveis em `.agents/skills/` e selecione apenas aquelas cujo `description` corresponda ao pedido.

As skills complementam este arquivo e devem conter os procedimentos detalhados de cada especialidade. Evite carregar ou combinar skills sem relacao direta com a tarefa. Em caso de conflito, as regras deste `AGENTS.md` prevalecem.

## Validacao

Execute validacoes proporcionais ao impacto:

- Mudanca localizada: execute primeiro o teste mais proximo da alteracao.
- Mudanca restrita a um contexto: use a solution contextual correspondente.
- Mudanca restrita ao Shared: use `PocArquitetura.Shared.slnx`.
- Mudanca transversal, arquitetural ou de governanca global: use `PocArquitetura.slnx`.
- Cobertura consolidada: execute `./test.ps1` ou `./test.sh` quando o escopo justificar.

Fluxo base, substituindo `<Solution>` pela solution adequada:

```bash
dotnet tool restore
dotnet restore ./<Solution>.slnx
dotnet build ./<Solution>.slnx --configuration Release --no-restore
dotnet test ./<Solution>.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Registre claramente qualquer validacao que nao possa ser executada e o motivo.

## Git e commits

- Nunca aplique alteracoes diretamente na branch `main`.
- Quando a tarefa exigir edicao e o repositorio estiver na `main`, crie ou use uma branch de trabalho relacionada ao objetivo da tarefa.
- Nao publique a branch, faca push ou abra pull request sem solicitacao explicita.
- Sempre que houver alteracao em arquivos do repositorio, crie commit semantico ao final da tarefa, salvo instrucao explicita para nao commitar.
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:` ou `ci:`.
- Antes de commitar, revise o diff e execute validacoes proporcionais ao impacto.
- Nao crie commit se houver falha de build ou teste sem registrar claramente o motivo.
- Quando houver arquivos `.cs` alterados, valide a formatacao apenas dos arquivos modificados e evite churn fora do escopo.
