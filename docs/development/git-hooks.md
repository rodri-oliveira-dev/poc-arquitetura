# Git hooks locais

O repositorio versiona hooks em `.githooks/`, mas a instalacao deixou de ocorrer durante o build. Builds, restores, testes e inicializacao das aplicacoes nao devem alterar configuracao local do Git.

Configure os hooks explicitamente durante o onboarding:

```bash
./scripts/setup/configure-git-hooks.sh
```

No Windows:

```powershell
./scripts/setup/configure-git-hooks.ps1
```

Os scripts configuram apenas `git config --local core.hooksPath .githooks`, validam a existencia de `.githooks/commit-msg`, `.githooks/post-merge` e `.githooks/pre-push`, nao exigem privilegios administrativos, nao usam configuracao global e nao executam nenhum hook durante a instalacao.

Para verificar sem alterar nada:

```bash
./scripts/setup/configure-git-hooks.sh --check
```

```powershell
./scripts/setup/configure-git-hooks.ps1 -Check
```

Se `core.hooksPath` ja estiver configurado com outro valor, os scripts falham sem sobrescrever. O valor atual pode apontar para hooks pessoais, corporativos ou de outras ferramentas; revise antes de substituir.

Para substituir conscientemente:

```bash
./scripts/setup/configure-git-hooks.sh --force
```

```powershell
./scripts/setup/configure-git-hooks.ps1 -Force
```

Para remover a configuracao local:

```bash
git config --local --unset core.hooksPath
```

No Linux/macOS, os scripts verificam o bit executavel dos hooks. Em modo de instalacao, eles aplicam `chmod +x` nos hooks obrigatorios quando necessario e registram a alteracao. Em modo `--check`, apenas reportam a ausencia de permissao e orientam o comando explicito.

## Hooks disponiveis

- `commit-msg`: valida a primeira linha da mensagem de commit com Conventional Commits.
- `post-merge`: apos `git merge` ou `git pull`, restaura as tools locais e as dependencias da solution.
- `pre-push`: executa validacoes locais leves quando houver alteracoes impactantes: Terraform `fmt -check` para arquivos Terraform, validacoes estaticas de Dockerfiles/Compose, restore, formatacao dos arquivos `.cs` alterados, build e testes unitarios rapidos sem cobertura. Para .NET, escolhe as solutions dos contextos impactados, `PocArquitetura.Shared.slnx` e/ou `PocArquitetura.slnx` conforme os arquivos alterados. Testes de integracao/container, cobertura, SonarQube, Trivy, Docker build, scan de imagens e Terraform validate completo ficam no Pull Request/GitHub Actions. Se `FULL_TESTS=true`, o hook reaproveita `./test.sh` para executar a validacao completa oficial com cobertura antes do push.

## Politica do post-merge

O `post-merge` executa automaticamente apos um merge local bem-sucedido, incluindo o fluxo comum de `git pull`.

Ele roda, a partir da raiz do repositorio:

```bash
dotnet tool restore
dotnet restore ./PocArquitetura.slnx
```

Isso reinstala ferramentas versionadas em `.config/dotnet-tools.json` quando necessario e atualiza o restore NuGet da solution apos receber mudancas de branch ou remoto.

## Politica do pre-push

Antes de executar validacoes, o `pre-push` tenta identificar os arquivos alterados entre o branch local e o upstream/remoto:

- em pushes de branches ja existentes no remoto, usa o intervalo informado pelo Git para comparar o SHA remoto atual com o SHA local enviado (`remote_sha..local_sha`);
- em pushes de branches novas, tenta calcular a base da branch usando `PRE_PUSH_BASE_REF`, quando informada, o upstream da propria branch, a configuracao local de tracking, o `HEAD` dos remotos conhecidos ou o melhor merge-base entre refs remotas disponiveis;
- em execucoes manuais sem entrada padrao do Git, aplica a mesma estrategia contra `HEAD`;
- se nenhuma base segura estiver disponivel, executa as validacoes por seguranca.

A responsabilidade de descobrir arquivos enviados fica no script reutilizavel `scripts/ci/collect-pre-push-files.py`. Ele le a entrada padrao do hook no formato `<local-ref> <local-sha> <remote-ref> <remote-sha>`, executa `git diff -C --find-copies-harder --name-status -z` somente sobre os commits enviados, deduplica registros repetidos entre multiplas refs e grava o resultado em arquivo temporario informado por `--output`. O formato do arquivo e uma sequencia de campos delimitados por NUL: `status\0path\0` para add/modify/delete e `status\0old-path\0new-path\0` para rename/copy. O hook tambem solicita `--paths-output` para gerar uma lista derivada de caminhos, usada apenas pela classificacao local de impacto.

Quando existem alteracoes em `*.tf` ou `*.tfvars`, o hook executa apenas `terraform fmt -check -recursive ./infra/terraform`, se a Terraform CLI estiver disponivel. Se a ferramenta nao existir localmente, o hook avisa e permite o push, porque o workflow `terraform-validation` executa a validacao completa no Pull Request.

O `pre-push` nao executa Trivy localmente por padrao. Os scans bloqueantes de Dockerfile, Terraform, misconfigurations, secrets e filesystem rodam no GitHub Actions pelo workflow `infrastructure-security` quando ha mudancas em Terraform, Dockerfiles, Compose, na action de Trivy ou no proprio workflow. Consulte [validacao de seguranca com Trivy](trivy-security-scan.md).

Cada arquivo alterado precisa terminar com uma classificacao explicita. O hook reconhece somente categorias conhecidas:

| Categoria | Exemplos | Validacao local |
| --- | --- | --- |
| Documentacao ou nao impactante | `README.md`, `docs/**`, `*.md`, imagens versionadas (`png`, `jpg`, `jpeg`, `gif`, `svg`, `webp`) | nenhuma |
| Contexto .NET conhecido | `src/<contexto>/**`, `tests/<contexto>/**`, `<Contexto>Service.slnx` para Audit, Balance, Identity, Ledger, Payment e Transfer | solution contextual |
| Configuracao .NET global | `global.json`, `NuGet.config`, `Directory.Build.*`, `Directory.Packages.props`, `.editorconfig`, `.globalconfig`, `.githooks/pre-push` | `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx` |
| Terraform | `*.tf`, `*.tfvars` | `terraform fmt -check -recursive ./infra/terraform` |
| Dockerfile | `Dockerfile`, `**/Dockerfile`, `Dockerfile.*`, `**/Dockerfile.*` | `ContainerBaselineValidator` |
| Docker Compose | `compose.yaml`, `compose.yml`, `compose.*.yaml`, `compose.*.yml` em qualquer diretorio | script oficial `validate-compose-configs.sh` |
| Manifesto de ferramentas | `.config/dotnet-tools.json`, `dotnet-tools.json` | `dotnet tool restore` |
| Validado somente no CI | `.github/workflows/**`, `.github/actions/**`, `coverlet.runsettings`, `test.sh`, `test.ps1` | nenhuma local; o log aponta o gate de PR |
| Desconhecido ou nao classificado | qualquer caminho que nao corresponda as regras anteriores | fallback conservador |

Nao existe regra generica de extensao desconhecida como documentacao. Se um arquivo desconhecido aparecer junto com Markdown ou `docs/**`, o diff deixa de ser documental e entra no fallback conservador.

Quando existem alteracoes em Dockerfiles, Compose ou nos scripts/configuracoes consumidos pela validacao local de containers, o hook executa somente validacoes leves:

| Alteracao | Validacao rapida local |
| --- | --- |
| `Dockerfile`, `**/Dockerfile`, `Dockerfile.*`, `**/Dockerfile.*` | `dotnet run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .` |
| `compose.yaml`, `compose.yml`, `compose.*.yaml`, `compose.*.yml` em qualquer diretorio | `./scripts/quality/containers/validate-compose-configs.sh` |
| `tools/ContainerBaselineValidator/**`, `.dockerignore`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` | `ContainerBaselineValidator` |
| `scripts/quality/containers/validate-compose-configs.*`, `scripts/lib/common.*`, `.env.local.example` | script oficial de validacao Compose |

O script oficial de Compose valida todas as combinacoes suportadas com `docker compose config --quiet`, sem `docker compose up`, sem inicializar containers e sem duplicar a matriz dentro do hook. Se a configuracao Compose for invalida, o push e bloqueado. Se houver violacao real do `ContainerBaselineValidator`, o push tambem e bloqueado.

Se `docker` ou `docker compose` nao estiver disponivel, a validacao local de Compose e ignorada com aviso explicito, sem mensagem de sucesso simulada; o gate bloqueante continua no Pull Request/GitHub Actions. Se o SDK .NET nao estiver disponivel em um diff que exige `ContainerBaselineValidator`, o hook avisa que o baseline local nao foi executado e deixa o gate bloqueante para o CI. O hook nao faz build de imagens, nao faz push de imagens, nao executa Trivy completo e nao sobe containers.

O hook executa restore, `dotnet format whitespace --verify-no-changes` somente para arquivos `.cs` alterados, build e testes unitarios rapidos sem cobertura quando encontra arquivos .NET impactantes. A escolha de solution e contextual:

| Alteracao | Validacao rapida local |
| --- | --- |
| Ledger | `LedgerService.slnx` |
| Balance | `BalanceService.slnx` |
| Payment | `PaymentService.slnx` |
| Transfer | `TransferService.slnx` |
| Identity | `IdentityService.slnx` |
| Audit | `AuditService.slnx` |
| Shared | `PocArquitetura.Shared.slnx` |
| Architecture.Tests | `PocArquitetura.slnx` |
| ComposeEnvGen | `LedgerService.slnx` |
| Event Contracts | Ledger + Balance |
| Global build/packages | Agregadora + Shared |
| Tool manifest | `dotnet tool restore` |
| Dockerfile | `ContainerBaselineValidator` |
| Docker Compose | script oficial `validate-compose-configs.sh` |
| Coverage config | nenhuma validacao rapida |
| docs-only | nenhuma validacao local |
| Terraform | `terraform fmt -check` |
| diff inseguro | Agregadora + Shared |
| arquivo desconhecido | fallback conservador |

Os caminhos de contexto sao `src/<contexto>/**`, `tests/<contexto>/**` e a solution do contexto. `tests/Architecture.Tests/**` permanece transversal e seleciona a solution agregadora. `tools/ComposeEnvGen/**` seleciona Ledger porque o tooling e necessario aos testes desse contexto.

Arquivos globais .NET incluem `global.json`, `NuGet.config`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` e `.githooks/pre-push`. Esses arquivos selecionam `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx`; se tambem houver arquivos de contexto no mesmo diff, a validacao global substitui as contextuais para evitar execucao redundante.

Configuracoes de analyzer como `.editorconfig` e `.globalconfig` tambem selecionam `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx`, mas nao inventam uma lista de arquivos C# para `dotnet format`. Quando a alteracao e somente desse tipo, o hook executa restore, build e formatacao contextual vazia/ignorada, mas pula testes rapidos.

Mudancas em `.config/dotnet-tools.json` ou `dotnet-tools.json` executam apenas `dotnet tool restore`. Mudancas isoladas em `coverlet.runsettings`, `test.sh` ou `test.ps1` nao disparam restore, build ou testes rapidos no modo padrao; cobertura e validacao completa continuam pertencendo a `./test.sh`, `./test.ps1` e ao Pull Request.

Quando apenas uma solution contextual e impactada, somente ela passa por restore, build e testes unitarios rapidos. Quando ha impacto em varios contextos, o hook acumula as respectivas solutions em ordem deterministica: Shared, Audit, Identity, Ledger, Balance, Payment, Transfer e agregadora. Arquivos `.cs` alterados sao formatados separadamente contra a solution do proprio contexto.

Exemplo de multiplos contextos:

```text
Ledger + Transfer
-> LedgerService.slnx
-> TransferService.slnx
```

Exemplo de Payment:

```text
src/payment/PaymentService.Application/Foo.cs
-> PaymentService.slnx
```

Exemplo de global + contexto:

```text
Directory.Packages.props + Ledger
-> PocArquitetura.Shared.slnx
-> PocArquitetura.slnx
```

`contracts/events/**` seleciona Ledger e Balance porque esses contexts produzem e consomem schemas versionados usados nos fluxos principais. Uma mudanca de source em Shared seleciona apenas `PocArquitetura.Shared.slnx` no pre-push porque os servicos consomem Shared por pacotes; a validacao de todos os servicos continua no fluxo global/PR quando aplicavel.

O hook pula restore, formatacao, build, testes e validacoes de containers quando todas as alteracoes sao claramente nao impactantes para validacao local, como Markdown, arquivos em `docs/` e imagens documentais reconhecidas (`png`, `jpg`, `jpeg`, `gif`, `svg`, `webp`).

Se houver mistura de documentacao com qualquer arquivo impactante, as validacoes rapidas sao executadas. Em caso de duvida, a regra e validar.

Quando um arquivo nao recebe classificacao, o hook registra cada caminho:

```text
==> pre-push: arquivo sem classificacao de impacto: caminho/do/arquivo
==> pre-push: executando validacoes conservadoras
```

O fallback conservador roda uma unica vez por push, mesmo com varios arquivos desconhecidos. Ele executa restore, build e testes unitarios rapidos sem cobertura de `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx`, executa as validacoes leves de Dockerfile e Compose, e aplica `terraform fmt -check` quando a Terraform CLI estiver disponivel. Esse fluxo nao executa cobertura, Testcontainers, testes de integracao, testes de contrato, SonarQube, Trivy completo, `terraform init`, `terraform validate`, build de imagens nem `dotnet format` com lista inventada de arquivos C#.

O detector do CI (`scripts/ci/detect-dotnet-impact.py`) permanece separado do hook local. A divergencia e intencional: no CI, o detector decide apenas impacto .NET para a matriz de PR e desconhecidos viram impacto agregado + Shared; no hook, a classificacao tambem cobre Terraform, Dockerfile, Compose, manifesto de ferramentas e `ci-only`, alem de preservar a execucao local leve. Os testes de `scripts/ci/tests/` cobrem os cenarios comuns para evitar drift perigoso: Payment conhecido nao cai no fallback, Markdown puro continua leve, arquivos desconhecidos acionam validacao conservadora e o coletor do `pre-push` preserva status, renames e copies em registros NUL.

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

Nesse modo, o hook delega para `./test.sh` com o `CONFIGURATION` e o `COVERAGE_THRESHOLD` configurados no ambiente. O padrao continua sendo `Release` e cobertura minima de `85%`. O modo completo continua delegado ao `./test.sh`; ele nao muda automaticamente para a matriz de solutions do modo rapido. Esse modo pode executar testes de integracao/container e, portanto, pode exigir Docker-compatible API.

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

Instalar/configurar os hooks no Linux/macOS:

```bash
./scripts/setup/configure-git-hooks.sh
```

Instalar/configurar os hooks no Windows:

```powershell
./scripts/setup/configure-git-hooks.ps1
```

Verificar a instalacao sem alterar configuracao:

```bash
./scripts/setup/configure-git-hooks.sh --check
```

```powershell
./scripts/setup/configure-git-hooks.ps1 -Check
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
dotnet format whitespace ./LedgerService.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-ledger-alterados>
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./BalanceService.slnx
dotnet format whitespace ./BalanceService.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-balance-alterados>
dotnet build ./BalanceService.slnx --configuration Release --no-restore
dotnet test ./BalanceService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PaymentService.slnx
dotnet format whitespace ./PaymentService.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-payment-alterados>
dotnet build ./PaymentService.slnx --configuration Release --no-restore
dotnet test ./PaymentService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PocArquitetura.Shared.slnx
dotnet format whitespace ./PocArquitetura.Shared.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-shared-alterados>
dotnet build ./PocArquitetura.Shared.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.Shared.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PocArquitetura.slnx
dotnet format whitespace ./PocArquitetura.slnx --verify-no-changes --no-restore --verbosity minimal --include <arquivos-cs-transversais-alterados>
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"
```

Para validar containers manualmente sem passar pelo hook:

```bash
dotnet run --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .
./scripts/quality/containers/validate-compose-configs.sh
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
  --scanners vuln,secret \
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

Os workflows `codeql-security-analysis` e `dependency-security-review` usam `paths-ignore` em `push` e/ou `pull_request` para nao rodar quando a mudanca contem apenas Markdown, arquivos em `docs/` ou imagens de documentacao. O workflow `main-dotnet-ci` nao usa `paths-ignore`, porque precisa sempre produzir o check obrigatorio `Build and test`; PRs documentais sao pulados dentro do job apos a deteccao centralizada.

Mudancas em codigo, projetos, solution, build, testes, Docker, workflows, hooks e configuracoes continuam acionando os workflows. O workflow CodeQL mantem a execucao agendada semanal independentemente de filtros de path.

## Falhas comuns

- Mensagem de commit invalida: ajuste a primeira linha para `type: descricao` ou `type(scope): descricao`.
- Build ou testes falhando: corrija o erro local antes de enviar o push.
- Cobertura abaixo de 85% em execucoes completas ou no CI: adicione ou ajuste testes para cobrir o comportamento alterado.
- Ferramentas POSIX indisponiveis: execute os hooks em ambiente compativel com Git Bash no Windows ou shell POSIX no Linux/macOS.
- Terraform CLI ausente: o `fmt` local e ignorado, mas o Pull Request executara a validacao completa. Instale as ferramentas descritas em [setup local Terraform e GCP](terraform-gcp-local-setup.md) para feedback antecipado.
- Trivy ausente: nao afeta o `pre-push`; o CI executara a validacao bloqueante quando houver mudancas cobertas pelo workflow de infraestrutura.
- Docker/Compose ausente: o hook avisa que a validacao local de Compose nao foi executada e nao imprime sucesso para essa etapa; o Pull Request continua com o gate bloqueante de container baseline.
- Docker desligado: o `pre-push` nao executa `docker compose up`, build de imagens nem Testcontainers. Testes `Integration`, `Container` e `Contract` ficam para o Pull Request ou execucao manual.

## Desabilitacao excepcional

Em caso excepcional, e assumindo o risco de enviar codigo sem as validacoes locais, use as opcoes nativas do Git:

```bash
git commit --no-verify
git push --no-verify
```

Use apenas para desbloqueio pontual. O CI e as revisoes continuam sendo a linha de defesa obrigatoria.
