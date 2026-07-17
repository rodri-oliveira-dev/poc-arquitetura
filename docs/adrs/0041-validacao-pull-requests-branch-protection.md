# ADR-0041: Validacao de Pull Requests e Branch Protection

## Status
Substituido

Substituida pela [ADR-0106](./0106-ci-principal-contextual-pull-requests-main.md).

## Data
2026-04-27

## Contexto
O repositorio ja possui workflows de GitHub Actions para build, testes, cobertura, CodeQL, revisao de dependencias e release automatica apos merge de PRs na `main`.

O fluxo de release nao executa build/testes novamente e depende de validacoes obrigatorias antes do merge. Tambem e necessario deixar claro qual check deve ser configurado como obrigatorio na protecao da branch `main`.

## Decisao
Criar o workflow `.github/workflows/pull-request-validation.yml` dedicado a validacao de PRs para qualquer branch.

O workflow executa restore da solution, build em Release e testes sem coleta de cobertura quando o PR contem arquivos impactantes.

Verificacao de vulnerabilidades, cobertura, relatorios e gate de cobertura permanecem nos workflows especificos, como `dependency-review` e `dotnet-ci`.

O gatilho inclui:

- `pull_request` para qualquer branch;
- `merge_group` com `checks_requested`, para suportar Merge Queue quando habilitada;
- `workflow_dispatch`, para execucao manual.

O workflow `dotnet-ci` passa a permanecer focado em validacao completa de `push` na `main` e execucao manual.

A branch `main` deve ser protegida no GitHub exigindo o status check `Build and test` antes do merge.

Como o check deve ser obrigatorio, o workflow de PR nao usa `paths-ignore`. Isso evita que o GitHub pule o workflow e deixe o required check pendente em PRs com alteracoes apenas documentais.

Para reduzir custo sem comprometer o status obrigatorio, o workflow detecta internamente os arquivos alterados em eventos `pull_request`. Se todos os arquivos forem documentacao ou imagens de documentacao (`*.md`, `docs/**`, `*.png`, `*.jpg`, `*.jpeg`, `*.gif`, `*.svg`, `*.webp`), o job reporta sucesso e pula restore, build e testes. Eventos `merge_group` e `workflow_dispatch` continuam executando a validacao completa.

## Consequencias

### Beneficios
- PRs possuem um check dedicado e estavel para build e testes.
- A protecao de branch pode exigir um status check claro antes do merge.
- O fluxo evita duplicar responsabilidades de cobertura, relatorios e vulnerabilidades em todo PR.
- O evento `merge_group` prepara o repositorio para Merge Queue sem criar outro workflow.
- PRs documentais tambem reportam o required check, evitando bloqueio por status pendente, mas sem executar restore, build e testes.
- PRs direcionados a branches diferentes de `main` tambem recebem o check de validacao.

### Trade-offs / custos
- A configuracao de branch protection nao e aplicada automaticamente pelo YAML e precisa ser feita no GitHub.
- O nome do check `Build and test` deve ser preservado ou atualizado na regra de protecao se for renomeado.
- Existe duplicacao intencional de restore da solution, build e testes entre `dotnet-ci` e `pull-request-validation`, pois esses passos sao o gate minimo de integridade.
- A deteccao de PR documental depende da lista de padroes mantida no workflow.

### Riscos
- Sem branch protection configurada, o workflow informa falhas mas nao impede merge.
- Se filtros de caminho forem adicionados futuramente ao workflow obrigatorio, PRs podem ficar bloqueados com check pendente.
- Se a lista de arquivos ignoraveis classificar incorretamente um arquivo como documental, o PR pode deixar de executar build e testes.

## Alternativas consideradas

1. **Manter apenas `dotnet-ci` em `pull_request` e `push`**
   - Menos arquivos, mas o mesmo workflow acumulava validacao de PR e validacao pos-merge, dificultando separar o check obrigatorio de PR.

2. **Criar workflow novo sem alterar `dotnet-ci`**
   - Atenderia ao PR, mas duplicaria build/testes em toda alteracao impactante.

3. **Tentar bloquear merge apenas por workflow**
   - Nao atende ao objetivo, porque GitHub Actions nao impede merge por si so. O bloqueio exige branch protection ou ruleset com required status checks.

## Proximos passos
- Configurar a protecao da branch `main` para exigir o status check `Build and test`.
- Reavaliar a lista de checks obrigatorios quando CodeQL, dependency review ou validacoes de documentacao passarem a ser requisitos de merge.
