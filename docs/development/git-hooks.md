# Git hooks locais

O repositorio versiona hooks em `.githooks/` e o build de `src/BalanceService.Api/BalanceService.Api.csproj` configura automaticamente:

```bash
git config core.hooksPath .githooks
```

O target e idempotente, roda apos o build, ignora CI (`CI=true`) e nao falha o build quando o comando `git` nao estiver disponivel ou quando a pasta nao for um repositorio Git.

## Hooks disponiveis

- `commit-msg`: valida a primeira linha da mensagem de commit com Conventional Commits.
- `pre-push`: executa restore, build, testes com cobertura e falha se a cobertura total de linhas ficar abaixo de 80% quando houver alteracoes impactantes.

## Politica do pre-push

Antes de executar validacoes pesadas, o `pre-push` tenta identificar os arquivos alterados entre o branch local e o upstream/remoto:

- em pushes normais, usa o intervalo informado pelo Git para comparar o SHA remoto com o SHA local;
- em execucao manual, compara `@{u}...HEAD`;
- se nao houver upstream/remoto configurado, ou se o diff nao puder ser calculado com seguranca, executa as validacoes completas.

O hook executa restore, build, testes e cobertura quando encontra qualquer arquivo impactante, incluindo:

- codigo, projetos e solution: `*.cs`, `*.csproj`, `*.sln`, `*.slnx`;
- configuracao de build/teste: `*.props`, `*.targets`, `*.runsettings`, `.editorconfig`, `global.json`, `NuGet.config`, `Directory.Build.*`, `Directory.Packages.props`, `dotnet-tools.json`, `coverlet.runsettings`;
- configuracoes conservadoras: `*.json`, `*.yml`, `*.yaml`, `*.ruleset`;
- Docker e compose: `Dockerfile`, `*/Dockerfile`, `compose.yaml`, `compose.*.yaml`;
- caminhos operacionais: `src/`, `tests/`, `.github/workflows/`, `.githooks/`, `scripts/`, `tools/`, `loadtests/k6/lib/`, `loadtests/k6/scenarios/`;
- scripts raiz usados por validacao: `test.sh` e `test.ps1`.

O hook pula build, testes e cobertura quando todas as alteracoes sao claramente nao impactantes, como Markdown, arquivos em `docs/`, imagens de documentacao (`png`, `jpg`, `jpeg`, `gif`, `svg`, `webp`), diagramas Mermaid/LikeC4 e notas textuais que nao entram no build.

Se houver mistura de documentacao com qualquer arquivo impactante, as validacoes completas sao executadas. Em caso de duvida, a regra e validar.

## Padrao de commit

Formato aceito:

```text
type(scope opcional): descricao
```

Tipos aceitos:

```text
feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
```

Exemplos validos:

```text
feat: add ledger endpoint
fix(balance): correct retry configuration
docs: update architecture notes
test(ledger): add idempotency tests
chore: update dependencies
```

Commits de merge e revert sao permitidos.

## Validacao manual

Instalar/configurar os hooks:

```bash
dotnet build src/BalanceService.Api/BalanceService.Api.csproj
git config --get core.hooksPath
```

Validar `commit-msg`:

```bash
printf "feat: add local hooks\n" > /tmp/commit-msg-ok
.githooks/commit-msg /tmp/commit-msg-ok

printf "Update files\n" > /tmp/commit-msg-invalid
.githooks/commit-msg /tmp/commit-msg-invalid
```

Validar `pre-push`:

```bash
.githooks/pre-push
```

O `pre-push` usa `coverlet.runsettings`, grava resultados em `TestResults/pre-push`, consolida a cobertura com ReportGenerator e valida o `Summary.txt`.

Para forcar a validacao completa manualmente, execute os comandos equivalentes:

```bash
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --collect:"XPlat Code Coverage" --settings ./coverlet.runsettings
```

## GitHub Actions

Os workflows `.github/workflows/dotnet.yml`, `.github/workflows/codeql.yml` e `.github/workflows/dependency-review.yml` usam `paths-ignore` em `push` e/ou `pull_request` para nao rodar quando a mudanca contem apenas Markdown, arquivos em `docs/` ou imagens de documentacao.

Mudancas em codigo, projetos, solution, build, testes, Docker, workflows, hooks e configuracoes continuam acionando os workflows. O workflow CodeQL mantem a execucao agendada semanal independentemente de filtros de path.

## Falhas comuns

- Mensagem de commit invalida: ajuste a primeira linha para `type: descricao` ou `type(scope): descricao`.
- Build ou testes falhando: corrija o erro local antes de enviar o push.
- Cobertura abaixo de 80%: adicione ou ajuste testes para cobrir o comportamento alterado.
- Ferramentas POSIX ou Python indisponiveis: execute os hooks em ambiente compativel com Git Bash no Windows ou shell POSIX no Linux/macOS, com `python3` ou `python` disponivel para ler o resumo de cobertura.

## Desabilitacao excepcional

Em caso excepcional, e assumindo o risco de enviar codigo sem as validacoes locais, use as opcoes nativas do Git:

```bash
git commit --no-verify
git push --no-verify
```

Use apenas para desbloqueio pontual. O CI e as revisoes continuam sendo a linha de defesa obrigatoria.
