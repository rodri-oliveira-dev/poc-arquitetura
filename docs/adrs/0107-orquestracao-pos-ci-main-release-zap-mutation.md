# ADR-0107: Orquestracao pos-CI da main para release, ZAP e mutation testing

## Status
Aceito

## Data
2026-07-14

## Contexto
O CI principal foi consolidado no workflow `main-dotnet-ci`, com o job/check estavel `Build and test`, conforme a ADR-0106.

Antes desta decisao, a release era criada diretamente a partir de `pull_request.closed` quando a PR era mergeada na `main`, e mutation testing tinha gatilho direto em `push` para `main`. O OWASP ZAP era apenas manual.

Esse desenho criava duas fragilidades operacionais:

- release podia ser iniciada por evento de PR mergeado, sem amarrar explicitamente a publicacao ao SHA aprovado pelo CI de `main`;
- mutation testing podia duplicar execucao em relacao a uma orquestracao futura pos-CI.

## Decisao
Orquestrar release, OWASP ZAP e mutation testing a partir do evento `workflow_run` do workflow `main-dotnet-ci`.

Os tres workflows escutam:

```yaml
workflow_run:
  workflows: ["main-dotnet-ci"]
  types: [completed]
  branches: ["main"]
```

Cada job automatico verifica explicitamente:

- `github.event.workflow_run.conclusion == 'success'`;
- `github.event.workflow_run.event == 'push'`;
- `github.event.workflow_run.head_branch == 'main'`.

Cada workflow faz checkout do SHA validado pelo CI:

```yaml
ref: ${{ github.event.workflow_run.head_sha }}
```

Release, OWASP ZAP e mutation testing partem do mesmo evento e nao usam `needs` entre si. Assim, ZAP e mutation executam em paralelo, e nenhum deles bloqueia a release.

## Release
O workflow `.github/workflows/release.yml` deixa de escutar `pull_request.closed` e passa a publicar somente apos sucesso do `main-dotnet-ci` na `main`.

A release:

- usa `github.event.workflow_run.head_sha` como SHA aprovado;
- calcula a versao via GitVersion sobre esse SHA;
- cria tag anotada apontando para esse mesmo SHA;
- cria GitHub Release com `--target` para esse mesmo SHA;
- preserva idempotencia quando a tag ja existe no mesmo SHA ou em outro SHA;
- usa `concurrency.cancel-in-progress: false`.

As notas da release tentam localizar a PR associada ao commit aprovado pela API do GitHub. Quando nao houver PR associada, a release ainda registra o commit aprovado e a lista de commits desde a tag SemVer anterior.

## OWASP ZAP
O workflow `.github/workflows/owasp-zap.yml` continua manual via `workflow_dispatch` e passa a rodar automaticamente apos sucesso do CI da `main`.

Na execucao automatica:

- alertas do ZAP sao consultivos;
- o parametro `--fail-on-alerts` nao e usado;
- falhas operacionais de restore, Docker, migrations, timeout, APIs indisponiveis ou runner ZAP continuam falhando o job;
- o workflow usa `permissions.contents: read`;
- o workflow nao deve ser required check em branch protection/rulesets.

Na execucao manual, o input `fail_on_alerts` permanece disponivel para transformar alertas em falha quando essa for a decisao operacional do operador.

## Mutation testing
O workflow `.github/workflows/mutation-tests.yml` deixa de ter gatilho direto em `push` para `main`, evitando duplicacao.

Ele continua manual via `workflow_dispatch` e passa a rodar automaticamente apos sucesso do CI da `main`.

Na execucao automatica:

- mutation score e consultivo;
- os arquivos `stryker-config.json` continuam com `thresholds.break` em `0`;
- falhas de restore, build ou execucao operacional do Stryker continuam vermelhas;
- o workflow usa `permissions.contents: read`;
- o workflow nao deve ser required check em branch protection/rulesets.

## Matriz esperada

| CI | Release | ZAP | Mutation |
| --- | --- | --- | --- |
| `success` | Inicia e pode criar tag/release para o SHA aprovado, respeitando idempotencia e SemVer. | Inicia em paralelo, com alertas consultivos e falhas operacionais vermelhas. | Inicia em paralelo, com score consultivo e falhas operacionais vermelhas. |
| `failure` | Job automatico nao executa. | Job automatico nao executa. | Job automatico nao executa. |
| `cancelled` | Job automatico nao executa. | Job automatico nao executa. | Job automatico nao executa. |

## Permissoes
Permissoes minimas por workflow:

- release: `contents: write` para tags/releases e `pull-requests: read` para localizar PR associada ao commit aprovado;
- OWASP ZAP: `contents: read`;
- mutation testing: `contents: read`.

Nenhum dos workflows pos-CI roda em `pull_request`, reduzindo exposicao de secrets privilegiados a codigo de PR nao confiavel.

## Consequencias

### Beneficios
- A publicacao passa a depender explicitamente de CI aprovado na `main`.
- Os tres workflows usam o mesmo SHA validado.
- ZAP e mutation testing ganham execucao automatica sem virarem gates de release.
- Falhas operacionais seguem visiveis em workflows separados.
- Mutation testing deixa de duplicar execucao por `push`.

### Trade-offs / custos
- Em CI falho ou cancelado, o evento `workflow_run` ainda pode criar uma run com job pulado; isso e esperado e evidencia que a guarda foi aplicada.
- As notas de release dependem da API de PRs associadas ao commit, que pode nao retornar resultado em alguns cenarios de push direto ou historico incomum.
- ZAP e mutation consomem runners apos cada CI bem-sucedido da `main`.

### Riscos
- Branch protection/rulesets precisam continuar exigindo `Build and test`, e nao ZAP/mutation/release.
- Se um ruleset exigir `owasp-zap-baseline` ou `mutation-tests`, achados consultivos ou falhas operacionais podem bloquear merge de forma contraria a esta decisao.
- Se o nome do workflow `main-dotnet-ci` mudar, os filtros `workflow_run` precisam ser atualizados juntos.

## Alternativas consideradas

1. **Manter release em `pull_request.closed`**
   - Rejeitado porque nao amarra a publicacao ao SHA validado pelo CI da `main`.

2. **Usar `push` direto para release, ZAP e mutation**
   - Rejeitado porque nao garante que a execucao parte da conclusao bem-sucedida do CI consolidado.

3. **Criar um workflow orquestrador unico com jobs release, ZAP e mutation**
   - Possivel, mas separaria menos claramente permissoes, artifacts e ownership operacional. Workflows separados com o mesmo `workflow_run` mantem paralelismo e isolamento.

4. **Usar `continue-on-error` no job inteiro de ZAP ou mutation**
   - Rejeitado porque esconderia falhas operacionais que devem permanecer vermelhas.
