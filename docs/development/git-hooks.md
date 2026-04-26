# Git hooks locais

O repositorio versiona hooks em `.githooks/` e o build de `src/BalanceService.Api/BalanceService.Api.csproj` configura automaticamente:

```bash
git config core.hooksPath .githooks
```

O target e idempotente, roda apos o build, ignora CI (`CI=true`) e nao falha o build quando o comando `git` nao estiver disponivel ou quando a pasta nao for um repositorio Git.

## Hooks disponiveis

- `commit-msg`: valida a primeira linha da mensagem de commit com Conventional Commits.
- `pre-push`: executa restore, build, testes com cobertura e falha se a cobertura total de linhas ficar abaixo de 80%.

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

O `pre-push` usa `coverlet.runsettings`, grava resultados em `TestResults/pre-push` e calcula a cobertura a partir dos arquivos `coverage.cobertura.xml`.

## Falhas comuns

- Mensagem de commit invalida: ajuste a primeira linha para `type: descricao` ou `type(scope): descricao`.
- Build ou testes falhando: corrija o erro local antes de enviar o push.
- Cobertura abaixo de 80%: adicione ou ajuste testes para cobrir o comportamento alterado.
- Ferramentas POSIX indisponiveis: execute os hooks em ambiente compativel com Git Bash no Windows ou shell POSIX no Linux/macOS.

## Desabilitacao excepcional

Em caso excepcional, e assumindo o risco de enviar codigo sem as validacoes locais, use as opcoes nativas do Git:

```bash
git commit --no-verify
git push --no-verify
```

Use apenas para desbloqueio pontual. O CI e as revisoes continuam sendo a linha de defesa obrigatoria.
