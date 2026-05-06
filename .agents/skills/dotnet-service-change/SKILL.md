---
name: dotnet-service-change
description: Use esta skill ao alterar servicos .NET deste repositorio: API, Application, Domain, Infrastructure, EF Core, Kafka, Outbox, autenticacao, configuracao ou testes relacionados. Nao use para mudancas apenas em governanca de agentes, CI/CD puro, release ou documentacao sem impacto no servico.
---

# Objetivo

Orientar alteracoes pequenas e seguras nos servicos .NET deste repositorio.
Esta skill preserva Clean Architecture, DDD, Central Package Management e as decisoes registradas em README, ADRs e documentacao tecnica.
Ela deve reduzir risco de violar fronteiras entre camadas ou alterar comportamento sem validacao proporcional.

# Quando usar

- Alteracoes em endpoints HTTP, controllers, middlewares, binds, mappers ou Swagger.
- Alteracoes em handlers, services, validators, DTOs de aplicacao ou casos de uso.
- Alteracoes em entidades, value rules, repositorios de dominio ou contratos de dominio.
- Alteracoes em EF Core, DbContext, configurations, migrations, transacoes ou repositories concretos.
- Alteracoes em Kafka, Outbox, DLQ, hosted services, idempotencia ou contratos de eventos.
- Alteracoes em autenticacao, autorizacao, scopes, policies, JWT, JWKS ou headers de seguranca.
- Criacao ou ajuste de testes ligados diretamente ao comportamento dos servicos.

# Quando nao usar

- Mudancas apenas em `.agents/`, `AGENTS.md`, prompts ou governanca de uso do Codex.
- Revisoes puramente de CI/CD, GitVersion, releases, hooks ou artifacts.
- Commits sem alteracao funcional pendente.
- Documentacao geral sem impacto em contrato, arquitetura, setup local ou comportamento dos servicos.

# Entradas esperadas

- Objetivo funcional ou tecnico da mudanca.
- Servico ou camada afetada, quando conhecido.
- Arquivos, erro, teste falhando, issue ou criterio de aceite relevante.
- Restricoes de compatibilidade, seguranca ou escopo informadas pelo usuario.

# Saidas esperadas

- Alteracao minima nos arquivos necessarios.
- Testes ajustados ou criados somente quando fizerem parte do escopo real.
- Documentacao ou ADR atualizada quando houver mudanca de contrato, fluxo, setup ou decisao.
- Relato final com arquivos alterados, validacoes executadas e riscos restantes.

# Passos

1. Analise o pedido e identifique area, servico e camada afetada.
2. Consulte `AGENTS.md`, README, ADRs e documentacao relevante antes de editar.
3. Localize implementacao, DI, contratos, configuracoes e testes relacionados.
4. Verifique impacto em contrato HTTP, autenticacao/autorizacao, EF Core, Kafka/Outbox, observabilidade e documentacao.
5. Decida se a mudanca exige ADR nova ou atualizacao de documentacao.
6. Aplique a menor alteracao coerente com os padroes existentes.
7. Preserve fronteiras: `Api` orquestra HTTP, `Application` coordena casos de uso, `Domain` guarda regras, `Infrastructure` concentra detalhes tecnicos.
8. Revise o diff e confirme que nao houve refactor, formatacao ou renomeacao fora do escopo.
9. Valide com comandos proporcionais ao impacto.
10. Relate resultado, validacoes e incertezas.

# Validacao

- Para mudancas pequenas e localizadas, execute o teste mais proximo quando existir.
- Para mudancas em contratos, DI, EF Core, Kafka, autenticacao ou comportamento transversal, prefira:

```powershell
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

- Quando adequado, use `./test.ps1` para validar cobertura conforme documentacao do projeto.
- Sempre execute `git status` antes de finalizar se houver edicoes.

# Restricoes

- Nao adicione `Version=` em `PackageReference`; use `Directory.Packages.props`.
- Nao mova regra de negocio para controller, endpoint, middleware ou infraestrutura.
- Nao coloque EF Core, Kafka, SQL, HTTP externo ou configuracao tecnica no `Domain`.
- Nao altere migrations existentes sem necessidade explicita.
- Nao altere testes apenas para faze-los passar.
- Nao relaxe seguranca sem instrucao explicita.
- Nao introduza segredos, URLs, portas ou comandos inventados.
- Nao faca push nem crie branch sem pedido explicito.
