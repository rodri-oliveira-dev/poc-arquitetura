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
- `pre-push`: executa validacoes locais leves quando houver alteracoes impactantes: Terraform `fmt -check` para arquivos Terraform, validacoes estaticas de Dockerfiles/Compose, restore, build e testes unitarios rapidos sem cobertura para as solutions .NET resolvidas a partir dos arquivos enviados no push. Testes de integracao/container, contratos, cobertura, SonarQube, Trivy, Docker build, scan de imagens e Terraform validate completo ficam no Pull Request/GitHub Actions. Se `FULL_TESTS=true`, o hook reaproveita `./test.sh` para executar a validacao completa oficial com cobertura antes do push.

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

A responsabilidade de descobrir arquivos enviados fica no script reutilizavel `scripts/ci/collect-pre-push-files.py`. Ele le a entrada padrao do hook no formato `<local-ref> <local-sha> <remote-ref> <remote-sha>`, executa `git diff -C --find-copies-harder --name-status -z` somente sobre os commits enviados, deduplica registros repetidos entre multiplas refs e grava o resultado em arquivo temporario informado por `--output`. O formato do arquivo e uma sequencia de campos delimitados por NUL: `status\0path\0` para add/modify/delete e `status\0old-path\0new-path\0` para rename/copy. O hook tambem solicita `--paths-output` para gerar uma lista derivada de caminhos, usada apenas para decidir se existem validacoes locais nao-.NET e se ha potencial impacto .NET.

A responsabilidade de resolver as solutions .NET impactadas fica no resolvedor reutilizavel `scripts/quality/resolve-solutions.cs`. O hook passa o arquivo temporario de alteracoes NUL para o resolvedor, que le as `.slnx`, identifica o projeto proprietario, aplica precedencia entre solution contextual, Shared e agregadora, considera renames/copies e preserva regras transversais que nao podem ser inferidas dos arquivos `.slnx`. O resultado e gravado em outro arquivo temporario com uma solution por linha. O `pre-push` remove os dois arquivos temporarios com `trap`, inclusive em falhas.

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

Quando encontra impacto .NET potencial, o hook resolve as solutions impactadas e mostra um resumo antes das validacoes:

```text
Solutions impactadas:
- BalanceService.slnx
- LedgerService.slnx
```

Para cada solution selecionada, o hook executa exatamente esta sequencia, em ordem deterministica e sem esconder a saida original do .NET:

```bash
dotnet restore <solution>
dotnet build <solution> --configuration "$CONFIGURATION" --no-restore
dotnet test <solution> --configuration "$CONFIGURATION" --no-build --no-restore --filter "$UNIT_TEST_FILTER"
```

Se uma etapa falhar, o hook interrompe imediatamente o push, retorna o mesmo codigo de saida do comando que falhou e adiciona um resumo curto antes de encerrar:

```text
pre-push bloqueado
etapa: build
solution: BalanceService.slnx
comando: dotnet build ./BalanceService.slnx --configuration Release --no-restore
```

A mensagem do hook identifica a etapa e a solution. A saida original do `dotnet restore`, `dotnet build` ou `dotnet test` continua visivel e identifica o projeto, arquivo, target ou teste exato que causou a falha.

A escolha de solution nao fica mais codificada no hook por flags de contexto. O resolvedor extrai a associacao projeto -> solution dos arquivos `.slnx` e mantem apenas regras explicitas para relacoes transversais ou decisoes arquiteturais que nao podem ser inferidas:

| Alteracao | Regra preservada |
| --- | --- |
| `tests/Architecture.Tests/**` | `PocArquitetura.slnx` |
| `contracts/events/**` | `LedgerService.slnx` e `BalanceService.slnx` |
| `tools/ComposeEnvGen/**` | `LedgerService.slnx` |
| `global.json`, `NuGet.config`, `Directory.Build.*`, `Directory.Packages.props`, `.editorconfig`, `.globalconfig`, `.githooks/pre-push` | `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx` |
| `.config/dotnet-tools.json`, `dotnet-tools.json` | `dotnet tool restore`; se houver outras mudancas .NET, o resolvedor tambem participa |
| diff inseguro ou arquivo desconhecido | fallback conservador com `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx` quando existirem |

Mudancas isoladas em `coverlet.runsettings`, `test.sh` ou `test.ps1` nao disparam restore, build ou testes rapidos no modo padrao; cobertura e validacao completa continuam pertencendo a `./test.sh`, `./test.ps1` e ao Pull Request.

Quando apenas uma solution contextual e impactada, somente ela passa por restore, build e testes unitarios rapidos. Quando ha impacto em varios contextos, o resolvedor deduplica e ordena as solutions uma unica vez por push: Shared, Audit, Identity, Ledger, Balance, Payment, Transfer e agregadora.

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

O hook pula restore, build, testes e validacoes de containers quando todas as alteracoes sao claramente nao impactantes para validacao local, como Markdown, arquivos em `docs/` e imagens documentais reconhecidas (`png`, `jpg`, `jpeg`, `gif`, `svg`, `webp`).

Se houver mistura de documentacao com qualquer arquivo impactante, as validacoes rapidas sao executadas. Em caso de duvida, a regra e validar.

Quando um arquivo nao recebe classificacao, o hook registra cada caminho:

```text
==> pre-push: arquivo sem classificacao de impacto: caminho/do/arquivo
==> pre-push: executando validacoes conservadoras
```

O fallback conservador roda uma unica vez por push, mesmo com varios arquivos desconhecidos. Ele executa restore, build e testes unitarios rapidos sem cobertura de `PocArquitetura.Shared.slnx` e `PocArquitetura.slnx`, executa as validacoes leves de Dockerfile e Compose, e aplica `terraform fmt -check` quando a Terraform CLI estiver disponivel. Esse fluxo nao executa cobertura, Testcontainers, testes de integracao, testes de contrato, SonarQube, Trivy completo, `terraform init`, `terraform validate`, build de imagens nem parsing textual da saida do MSBuild.

O detector do CI (`scripts/ci/detect-dotnet-impact.py`) permanece separado do hook local. A divergencia e intencional: no CI, o detector decide apenas impacto .NET para a matriz de PR; no hook, a classificacao tambem cobre Terraform, Dockerfile, Compose, manifesto de ferramentas e `ci-only`, enquanto o resolvedor de solutions reaproveitavel centraliza a selecao .NET. Os testes de `scripts/ci/tests/` cobrem os cenarios comuns para evitar drift perigoso: uma ou varias solutions, deduplicacao, ausencia de solution .NET, falhas de restore/build/test, primeira falha, branch nova/existente, multiplas refs e rename entre contextos.

Os testes locais do `pre-push` usam o filtro:

```bash
Category!=Integration&Category!=Container&Category!=Contract
```

Isso evita executar testes de integracao, contrato ou container no push local. O hook nao depende de Docker ligado: testes baseados em Testcontainers/PostgreSQL e testes opcionais de emulador ficam para o PR ou execucao manual explicita.

As validacoes nao-.NET e a validacao completa explicita registram duracao aproximada em segundos. O fluxo .NET rapido preserva a saida original do SDK e acrescenta somente o resumo de bloqueio quando uma etapa falha.

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

Para executar a validacao rapida manualmente sem passar pelo hook, use os comandos equivalentes para cada solution impactada:

```bash
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./BalanceService.slnx
dotnet build ./BalanceService.slnx --configuration Release --no-restore
dotnet test ./BalanceService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PaymentService.slnx
dotnet build ./PaymentService.slnx --configuration Release --no-restore
dotnet test ./PaymentService.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PocArquitetura.Shared.slnx
dotnet build ./PocArquitetura.Shared.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.Shared.slnx --configuration Release --no-build --no-restore --filter "Category!=Integration&Category!=Container&Category!=Contract"

dotnet restore ./PocArquitetura.slnx
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
