---
name: integration-tests-dotnet
description: Use esta skill para criar ou revisar testes de integracao .NET neste repositorio, incluindo WebApplicationFactory, fixtures, EF InMemory, Testcontainers PostgreSQL e isolamento. Nao use para testes unitarios simples ou mudancas de producao sem teste de integracao.
---

# Objetivo

Orientar testes de integracao dos servicos .NET deste repositorio com foco em fidelidade, isolamento e custo de execucao.
A skill ajuda a decidir quando manter `WebApplicationFactory` leve, quando usar EF InMemory e quando a dependencia real com PostgreSQL via Testcontainers justifica o custo.
Ela preserva a estrategia incremental descrita nas ADRs e evita transformar testes de integracao em smoke tests distribuidos caros sem necessidade.

# Quando usar

- Criar ou revisar testes em `tests/*IntegrationTests`.
- Alterar factories baseadas em `WebApplicationFactory`.
- Avaliar uso de EF InMemory versus PostgreSQL real.
- Introduzir ou revisar Testcontainers PostgreSQL em escopo pequeno.
- Testar endpoints HTTP, autenticacao/autorizacao, readiness, migrations, constraints, queries ou transacoes.
- Ajustar fixtures, seeds, limpeza de banco ou desligamento de hosted services em testes.

# Quando nao usar

- Testes unitarios puros de Domain, Application, mappers ou validators.
- Mudancas sem interacao HTTP, persistencia real, DI completo ou infraestrutura de teste.
- Testes e2e com Kafka, Compose ou Aspire sem decisao arquitetural explicita.
- Alteracoes em pipelines de teste sem mudar a estrategia de testes de integracao.

# Entradas esperadas

- Servico, endpoint, comportamento ou bug a validar.
- Projeto de teste afetado.
- Dependencias necessarias: HTTP, banco, autenticacao, Kafka ou hosted services.
- Criterio de fidelidade esperado pelo usuario ou pela ADR aplicavel.

# Saidas esperadas

- Testes de integracao focados e legiveis.
- Factory ou fixture ajustada sem afetar outros projetos indevidamente.
- Seed e limpeza de dados previsiveis.
- Justificativa quando Testcontainers for usado ou evitado.
- Validacao por projeto de teste ou pela solucao quando o impacto for transversal.

# Passos

1. Identifique se o teste exige pipeline HTTP real, DI completo, provider de banco real ou apenas comportamento isolado.
2. Consulte `AGENTS.md`, README, `docs/development/test-coverage.md` e ADRs relevantes antes de mudar a estrategia.
3. Localize a factory existente do servico e preserve seu padrao quando suficiente.
4. Desligue hosted services externos em testes quando eles nao forem parte do comportamento validado.
5. Use EF InMemory apenas quando constraints, SQL, transacoes, migrations e provider Npgsql nao forem o alvo do teste.
6. Use Testcontainers PostgreSQL somente quando a fidelidade do provider real aumentar claramente a confianca.
7. Mantenha containers, seeds e limpeza com ciclo de vida explicito e previsivel.
8. Evite dependencias externas nao controladas e portas fixas inventadas.
9. Revise se o teste cobre o risco real sem aumentar flakiness ou tempo desnecessariamente.
10. Documente impacto quando a estrategia oficial de testes mudar.

# Validacao

- Execute o projeto de teste afetado quando possivel:

```powershell
dotnet test ./tests/<Projeto>.csproj --configuration Release
```

- Para mudancas transversais em testes, use:

```powershell
dotnet test ./LedgerService.slnx --configuration Release --settings ./coverlet.runsettings
```

- Quando alterar cobertura ou estrategia oficial, valide a documentacao relacionada.
- Execute `git status` antes de finalizar se houver edicoes.

# Restricoes

- Nao alterar testes apenas para ocultar falha real.
- Nao tornar Testcontainers requisito amplo sem ADR ou documentacao correspondente.
- Nao usar Compose, Aspire, Kafka real ou rede externa como dependencia casual de teste.
- Nao introduzir sleeps arbitrarios; prefira sincronizacao deterministica.
- Nao incluir segredos, portas inventadas ou configuracao local nao documentada.
- Nao fazer push nem criar branch.
