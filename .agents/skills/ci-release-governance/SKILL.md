---
name: ci-release-governance
description: Use esta skill para revisar ou ajustar GitHub Actions, GitVersion, releases, hooks, cobertura, mutation testing e automacoes seguras deste repositorio. Nao use para mudancas em codigo de producao ou testes de aplicacao sem impacto no pipeline.
---

# Objetivo

Orientar mudancas em CI/CD, versionamento, releases e automacoes do repositorio com seguranca e rastreabilidade.
Esta skill concentra regras de GitHub Actions, GitVersion, hooks locais, artifacts, cobertura, testes de carga e mutation testing sem poluir `AGENTS.md`.
Ela deve evitar drift entre pipeline, README, docs de desenvolvimento e ADRs.

# Quando usar

- Alterar ou revisar workflows em `.github/workflows/`.
- Ajustar `GitVersion.yml`, convencoes de release ou mensagens semanticas.
- Revisar `.githooks/`, validacoes locais, artifacts, cobertura, testes de carga ou mutation testing.
- Avaliar hardening de automacoes, permissoes, retencao, publicacao ou triggers.
- Atualizar documentacao de desenvolvimento relacionada a CI/CD ou release.
- Alterar triggers, execucao, thresholds, artifacts ou estrategia de testes de carga.

# Quando nao usar

- Alteracoes funcionais nos servicos .NET.
- Testes unitarios ou de integracao sem mudanca em pipeline/automacao.
- Commits locais simples quando as convencoes ja estiverem claras.
- Deploy ou publish real, salvo pedido explicito e ambiente documentado.

# Entradas esperadas

- Workflow, hook, arquivo de versionamento ou automacao alvo.
- Problema observado, objetivo de governanca ou criterio de aceite.
- Restricoes de seguranca, permissao, trigger, artifact ou compatibilidade.
- Documentacao ou ADR relacionada quando houver.

# Saidas esperadas

- Ajuste minimo em pipeline, versionamento, hook ou documentacao.
- Explicacao do impacto em build, test, coverage, release ou seguranca.
- Validacao local possivel sem executar deploy/publish.
- Commit semantico quando solicitado.

# Passos

1. Identifique se a mudanca e CI, release, versionamento, hook, coverage, mutation testing, testes de carga ou seguranca de automacao.
2. Consulte `AGENTS.md`, README, `docs/development/`, ADRs relevantes, `GitVersion.yml` e workflows afetados.
3. Compare o comportamento documentado com o comportamento configurado.
4. Preserve comandos oficiais e Central Package Management.
5. Reduza permissoes, escopos e artifacts somente com criterio explicito.
6. Evite triggers amplos ou jobs caros sem necessidade comprovada.
7. Testes de carga nao devem rodar em todo build, PR ou validacao local padrao.
8. Quando existirem workflows de load test, mantenha execucao manual, agendada, informativa ou restrita a cenarios explicitamente definidos.
9. Atualize documentacao quando mudar fluxo oficial, requisito local, release, validacao ou estrategia de testes de carga.
10. Valide sintaxe e consistencia dos arquivos alterados.
11. Revise diff e confirme que nao houve alteracao de codigo de producao ou testes de aplicacao.
12. Relate impacto, validacoes e riscos.

# Validacao

- Para mudancas em versionamento, rode quando disponivel:

```powershell
dotnet tool restore
dotnet gitversion
```

- Para mudancas que afetam build/test, valide proporcionalmente:

```powershell
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
```

- Revise YAML, links locais e documentacao afetada.
- Execute `git status` antes de finalizar se houver edicoes.

# Restricoes

- Nao executar publish, deploy, release real ou push.
- Nao criar branch sem pedido explicito.
- Nao ampliar permissoes de workflow sem justificativa clara.
- Nao remover validacoes de seguranca, cobertura, testes ou cenarios de carga para contornar falha.
- Nao alterar codigo de producao ou testes de aplicacao fora do escopo.
- Nao introduzir segredos em workflows, scripts ou documentacao.
