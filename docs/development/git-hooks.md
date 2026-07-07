# Git hooks locais

O repositorio versiona hooks em `.githooks/` e o build de `src/balance/BalanceService.Api/BalanceService.Api.csproj` configura automaticamente:

```bash
git config core.hooksPath .githooks
```

O target e idempotente, roda apos o build, ignora CI (`CI=true`) e nao falha o build quando o comando `git` nao estiver disponivel ou quando a pasta nao for um repositorio Git.

## Hooks disponiveis

- `commit-msg`: valida a primeira linha da mensagem de commit com Conventional Commits.
- `post-merge`: apos `git merge` ou `git pull`, restaura as tools locais e as dependencias da solution.
- `pre-push`: executa validacoes locais leves quando houver alteracoes impactantes: Terraform `fmt -check` para arquivos Terraform, restore, formatacao dos arquivos `.cs` alterados, build e testes unitarios rapidos sem cobertura. Para .NET, escolhe `LedgerService.slnx`, `PocArquitetura.Shared.slnx` ou ambas conforme os arquivos alterados. Testes de integracao/container, cobertura, SonarQube, Trivy e Terraform validate completo ficam no Pull Request/GitHub Actions. Se `FULL_TESTS=true`, o hook reaproveita `./test.sh` para executar a validacao completa oficial com cobertura antes do push.

## Politica do post-merge

O `post-merge` executa automaticamente apos um merge local bem-sucedido, incluindo o fluxo comum de `git pull`.

Ele roda, a partir da raiz do repositorio:

```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
```

Isso reinstala ferramentas versionadas em `.config/dotnet-tools.json` quando necessario e atualiza o restore NuGet da solution apos receber mudancas de branch ou remoto.

## Politica do pre-push

Antes de executar validacoes, o `pre-push` tenta identificar os arquivos alterados entre o branch local e o upstream/remoto:

- em pushes de branches ja existentes no remoto, usa o intervalo informado pelo Git para comparar o SHA remoto atual com o SHA local enviado (`remote_sha..local_sha`);
- em pushes de branches novas, tenta calcular a base da branch usando o upstream da propria branch, a configuracao local de tracking, o `HEAD` dos remotos conhecidos ou o melhor merge-base entre refs remotas disponiveis;
- em execucoes manuais sem entrada padrao do Git, aplica a mesma estrategia contra `HEAD`;
- se nenhuma base segura estiver disponivel, executa as validacoes por seguranca.

Quando existem alteracoes em `*.tf` ou `*.tfvars`, o hook executa apenas `terraform fmt -check -recursive ./infra/terraform`, se a Terraform CLI estiver disponivel. Se a ferramenta nao existir localmente, o hook avisa e permite o push, porque o workflow `infra-security-and-terraform-validation` executa a validacao completa no Pull Request.

O `pre-push` nao executa Trivy localmente por padrao. Os scans bloqueantes de Dockerfile, Terraform, misconfigurations, secrets e filesystem rodam no GitHub Actions pelo workflow `infra-security-and-terraform-validation` quando ha mudancas em Terraform, Dockerfiles, Compose, na action de Trivy ou no proprio workflow. Consulte [validacao de seguranca com Trivy](trivy-security-scan.md).

O hook executa restore, `dotnet format whitespace --verify-no-changes` somente para arquivos `.cs` alterados, build e testes unitarios rapidos sem cobertura quando encontra arquivos .NET impactantes. A escolha de solution e feita assim:

- alteracoes em `src/Shared/`, `tests/Shared/` ou `PocArquitetura.Shared.slnx` validam `PocArquitetura.Shared.slnx`;
- alteracoes em `src/audit/`, `src/identity/`, `src/balance/`, `src/transfer/`, `src/ledger/`, `tests/audit/`, `tests/identity/`, `tests/balance/`, `tests/transfer/`, `tests/ledger/`, `tests/Architecture.Tests/` ou `LedgerService.slnx` validam `LedgerService.slnx`;
- alteracoes globais em `global.json`, `NuGet.config`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `dotnet-tools.json`, `.config/dotnet-tools.json`, `.editorconfig`, `.globalconfig`, `coverlet.runsettings`, `test.sh`, `test.ps1`, `.github/actions/setup-dotnet/` ou `.githooks/pre-push` validam ambas;
- alteracoes em `src/Shared/Directory.Build.props` ou `src/Shared/Directory.Packages.props` validam Shared;
- quando o diff nao pode ser determinado com seguranca, ambas as solutions sao validadas.

Quando apenas uma solution e impactada, somente ela passa por restore, build e testes unitarios rapidos. Quando ha impacto em Shared e servicos, o hook executa as duas validacoes. Arquivos `.cs` alterados sao formatados separadamente por solution: Shared usa `PocArquitetura.Shared.slnx` e servicos usam `LedgerService.slnx`.

O hook pula restore, formatacao, build e testes quando todas as alteracoes sao claramente nao impactantes para validacao local, como Markdown, arquivos em `docs/`, imagens de documentacao (`png`, `jpg`, `jpeg`, `gif`, `svg`, `webp`), diagramas Mermaid/LikeC4 e notas textuais que nao entram no build. Mudancas em Dockerfile, Compose e Trivy sao validadas no Pull Request pelo workflow de infraestrutura quando seus filtros se aplicam.

Se houver mistura de documentacao com qualquer arquivo impactante, as validacoes rapidas sao executadas. Em caso de duvida, a regra e validar.

Quando o diff contem ate 30 arquivos C#, o hook divide a verificacao de
formatacao em lotes para evitar limites locais de tamanho da linha de comando,
mantendo a mesma regra de falha se qualquer arquivo estiver fora do padrao.
Acima desse limite, a formatacao .NET local e ignorada para preservar o push
como feedback leve; build, testes rapidos e os gates do Pull Request continuam
validando a branch. O limite pode ser ajustado temporariamente com
`DOTNET_FORMAT_FILE_LIMIT`.

Os testes locais do `pre-push` usam o filtro:

```bash
Category!=Integration&Category!=Container&Category!=Contract
```

Isso evita executar testes de integracao, contrato ou container no push local. O hook nao depende de Docker ligado: testes baseados em Testcontainers/PostgreSQL e testes opcionais de emulador ficam para o PR ou execucao manual explicita.

Cada etapa executada pelo hook registra a duracao aproximada em segundos. Esse log ajuda a identificar gargalos locais sem adicionar dependencia externa.

Para executar a validacao completa oficial durante o push, use:

```bash
FULL_TESTS=true git push
```

Nesse modo, depois do restore e da etapa local de formatacao dos arquivos `.cs` alterados quando ela estiver dentro do limite, o hook executa `./test.sh` com o `CONFIGURATION` e o `COVERAGE_THRESHOLD` configurados no ambiente. O padrao continua sendo `Release` e cobertura minima de `85%`. O modo completo continua delegado ao `./test.sh`; ele nao muda automaticamente para a matriz de solutions do modo rapido. Esse modo pode executar testes de integracao/container e, portanto, pode exigir Docker-compatible API.

## Padrao de commit

Formato aceito:

```text
type(scope opcional): descricao
type(scope opcional)!: descricao
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
feat!: change public API contract
```

Use `!` antes de `:` para declarar breaking change na primeira linha do commit. Commits de merge e revert sao permitidos.

## Validacao manual

Instalar/configurar os hooks:

```bash
dotnet build src/balance/BalanceService.Api/BalanceService.Api.csproj
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

Por padrao, essa execucao manual roda apenas validacoes locais leves sem cobertura. Para executar a validacao completa com cobertura:

```bash
./test.sh
```

Ou, durante o push:

```bash
FULL_TESTS=true git push
```

Validar `post-merge` manualmente:

```bash
.githooks/post-merge
```

Para executar a validacao rapida manualmente sem passar pelo hook, use os comandos equivalentes:

```bash
dotnet restore ./LedgerService.slnx
dotnet format whitespace ./LedgerService.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-alterados>
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PocArquitetura.Shared.slnx
dotnet format whitespace ./PocArquitetura.Shared.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-shared-alterados>
dotnet build ./PocArquitetura.Shared.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.Shared.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"
```

Para a validacao completa com cobertura, use `./test.sh` ou `./test.ps1`. Esses comandos podem executar testes de integracao/container e, portanto, precisam de Docker-compatible API quando a suite completa exigir Testcontainers.

Para executar apenas a validacao Trivy manualmente, use os mesmos comandos de scan do CI:

```bash
trivy config \
  --severity HIGH,CRITICAL \
  --tf-vars infra/terraform/environments/dev/validation.tfvars \
  --skip-dirs node_modules \
  --skip-dirs .git \
  --skip-dirs dist \
  --skip-dirs .terraform \
  --skip-dirs bin \
  --skip-dirs "**/bin" \
  --skip-dirs obj \
  --skip-dirs "**/obj" \
  --skip-dirs .vs \
  --skip-dirs .idea \
  --skip-dirs TestResults \
  --skip-dirs "**/TestResults" \
  --skip-dirs coverage \
  --skip-dirs CodeCoverage \
  --skip-dirs StrykerOutput \
  --skip-dirs .dotnet \
  --skip-dirs .dotnet-home \
  --skip-dirs .nuget \
  --skip-dirs artifacts \
  --skip-dirs infra/nginx/certs \
  .
trivy fs \
  --scanners vuln,secret,misconfig \
  --severity HIGH,CRITICAL \
  --tf-vars infra/terraform/environments/dev/validation.tfvars \
  --skip-dirs node_modules \
  --skip-dirs .git \
  --skip-dirs dist \
  --skip-dirs .terraform \
  --skip-dirs bin \
  --skip-dirs "**/bin" \
  --skip-dirs obj \
  --skip-dirs "**/obj" \
  --skip-dirs .vs \
  --skip-dirs .idea \
  --skip-dirs TestResults \
  --skip-dirs "**/TestResults" \
  --skip-dirs coverage \
  --skip-dirs CodeCoverage \
  --skip-dirs StrykerOutput \
  --skip-dirs .dotnet \
  --skip-dirs .dotnet-home \
  --skip-dirs .nuget \
  --skip-dirs artifacts \
  --skip-dirs infra/nginx/certs \
  .
```

## GitHub Actions

Os workflows `main-dotnet-ci`, `codeql-security-analysis` e `dependency-security-review` usam `paths-ignore` em `push` e/ou `pull_request` para nao rodar quando a mudanca contem apenas Markdown, arquivos em `docs/` ou imagens de documentacao.

Mudancas em codigo, projetos, solution, build, testes, Docker, workflows, hooks e configuracoes continuam acionando os workflows. O workflow CodeQL mantem a execucao agendada semanal independentemente de filtros de path.

## Falhas comuns

- Mensagem de commit invalida: ajuste a primeira linha para `type: descricao` ou `type(scope): descricao`.
- Build ou testes falhando: corrija o erro local antes de enviar o push.
- Cobertura abaixo de 85% em execucoes completas ou no CI: adicione ou ajuste testes para cobrir o comportamento alterado.
- Ferramentas POSIX indisponiveis: execute os hooks em ambiente compativel com Git Bash no Windows ou shell POSIX no Linux/macOS.
- Terraform CLI ausente: o `fmt` local e ignorado, mas o Pull Request executara a validacao completa. Instale as ferramentas descritas em [setup local Terraform e GCP](terraform-gcp-local-setup.md) para feedback antecipado.
- Trivy ausente: nao afeta o `pre-push`; o CI executara a validacao bloqueante quando houver mudancas cobertas pelo workflow de infraestrutura.
- Docker desligado: o `pre-push` continua executando apenas testes unitarios. Testes `Integration`, `Container` e `Contract` ficam para o Pull Request ou execucao manual.

## Desabilitacao excepcional

Em caso excepcional, e assumindo o risco de enviar codigo sem as validacoes locais, use as opcoes nativas do Git:

```bash
git commit --no-verify
git push --no-verify
```

Use apenas para desbloqueio pontual. O CI e as revisoes continuam sendo a linha de defesa obrigatoria.
