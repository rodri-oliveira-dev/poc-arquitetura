# ADR-0106: CI principal contextual para Pull Requests e main

## Status
Aceito

## Data
2026-07-14

## Contexto
O repositorio possuia dois workflows com responsabilidades parcialmente sobrepostas:

- `.github/workflows/pull-request-validation.yml`, com restore, build e testes para PRs e Merge Queue;
- `.github/workflows/dotnet.yml`, com restore, auditoria NuGet, SonarQube Cloud, build, testes, cobertura, relatorios e artifacts.

Essa divisao preservava um required check estavel para PRs, mas duplicava restore, build e testes em PRs impactantes. Tambem havia `.github/workflows/sonarqube-context.yml`, reutilizavel, mas sem participacao efetiva no fluxo oficial.

## Decisao
Consolidar o CI principal em `.github/workflows/dotnet.yml`, mantendo o workflow `main-dotnet-ci` e o job/check `Build and test`.

O workflow roda em:

- `pull_request`;
- `merge_group` com `checks_requested`;
- `push` na `main`;
- `workflow_dispatch`.

O workflow usa exclusivamente `scripts/ci/detect-dotnet-impact.py` para decidir, em PRs, se deve executar:

- somente `aggregate`, com `PocArquitetura.slnx`;
- somente `shared`, com `PocArquitetura.Shared.slnx`;
- ambos, para arquivos globais ou falha de deteccao;
- nenhum restore/build/test em PR somente documental.

Em `merge_group`, `push` na `main` e `workflow_dispatch`, o comportamento e conservador e executa aggregate e Shared.

A estrategia de SonarQube Cloud passa a ser contextual por aggregate e Shared dentro do proprio `dotnet.yml`:

- `aggregate`: projeto `rodri-oliveira-dev_poc-arquitetura`;
- `shared`: projeto `rodri-oliveira-dev_poc-arquitetura-shared`.

O workflow reutilizavel `.github/workflows/sonarqube-context.yml` foi removido para eliminar codigo orfao e evitar uma segunda implementacao de restore, build, test, cobertura, Sonar begin/end e relatorios.

O workflow `.github/workflows/pull-request-validation.yml` tambem foi removido. Branch protection deve continuar exigindo o check `Build and test`, agora produzido por `.github/workflows/dotnet.yml`.

## Consequencias

### Beneficios
- Cada PR executa restore, build e testes uma unica vez no CI principal.
- PRs documentais continuam produzindo check verde sem build.
- Merge Queue permanece suportada.
- Push na `main` e execucao manual continuam validados de forma conservadora.
- Auditoria NuGet, cobertura, thresholds, SonarQube Cloud, relatorios e artifacts ficam em um unico workflow.
- A duplicacao entre `pull-request-validation.yml`, `dotnet.yml` e `sonarqube-context.yml` foi removida.

### Trade-offs / custos
- O workflow `main-dotnet-ci` ficou mais denso porque concentra deteccao, validacao contextual e relatorios.
- O contexto Shared passa a ter Quality Gate e cobertura proprios no CI, exigindo que o projeto remoto `rodri-oliveira-dev_poc-arquitetura-shared` esteja configurado no SonarQube Cloud.
- Rulesets externos que amarravam o required check ao workflow antigo podem precisar de ajuste manual no GitHub, mesmo com o nome `Build and test` preservado.

### Riscos
- Se o projeto SonarQube Cloud Shared nao existir ou nao tiver Quality Gate configurado, PRs Shared falharao ate a configuracao remota ser ajustada.
- Pull requests vindos de forks nao recebem `SONAR_TOKEN`; nesses casos, o workflow pula SonarQube Cloud para nao expor secrets e mantem restore, auditoria NuGet, build, testes e cobertura.
- Sem branch protection configurada, o workflow informa falhas mas nao impede merge.

## Alternativas consideradas

1. **Manter o workflow de PR rapido e remover `pull_request` do `dotnet.yml`**
   - Reduziria custo em PRs, mas nao atenderia ao objetivo de manter cobertura, auditoria NuGet e Sonar no CI principal de PR sem duplicacao.

2. **Usar `.github/workflows/sonarqube-context.yml` como workflow reutilizavel**
   - Evitaria parte do YAML inline, mas manteria restore, build, test, cobertura e relatorios implementados fora do CI principal. A decisao foi concentrar o fluxo em um unico workflow.

3. **Manter SonarQube apenas global**
   - Preservaria o projeto historico, mas criaria conflito com a regra de executar somente Shared quando apenas Shared for impactado. A analise contextual por aggregate e Shared deixa a estrategia explicita.

## Proximos passos
- Confirmar no GitHub que branch protection/rulesets exigem `Build and test` produzido pelo workflow `main-dotnet-ci`.
- Confirmar no SonarQube Cloud que o projeto `rodri-oliveira-dev_poc-arquitetura-shared` existe, usa CI Analysis e possui Quality Gate esperado.
