# Validacao de seguranca com Trivy

O repositorio usa Trivy para feedback antecipado sobre configuracoes de infraestrutura, Dockerfiles, secrets e vulnerabilidades detectaveis no filesystem versionado.

## O que e validado

A composite action `.github/actions/trivy-repository-scan`, chamada pelo workflow `.github/workflows/infrastructure-security.yml`, executa:

```powershell
trivy config `
  --severity HIGH,CRITICAL `
  --tf-vars infra/terraform/environments/dev/validation.tfvars `
  --skip-dirs node_modules `
  --skip-dirs .git `
  --skip-dirs dist `
  --skip-dirs .terraform `
  --skip-dirs bin `
  --skip-dirs "**/bin" `
  --skip-dirs obj `
  --skip-dirs "**/obj" `
  --skip-dirs .vs `
  --skip-dirs .idea `
  --skip-dirs TestResults `
  --skip-dirs "**/TestResults" `
  --skip-dirs coverage `
  --skip-dirs CodeCoverage `
  --skip-dirs StrykerOutput `
  --skip-dirs .dotnet `
  --skip-dirs .dotnet-home `
  --skip-dirs .nuget `
  --skip-dirs artifacts `
  --skip-dirs infra/nginx/certs `
  .
trivy fs `
  --scanners vuln,secret `
  --severity HIGH,CRITICAL `
  --tf-vars infra/terraform/environments/dev/validation.tfvars `
  --skip-dirs node_modules `
  --skip-dirs .git `
  --skip-dirs dist `
  --skip-dirs .terraform `
  --skip-dirs bin `
  --skip-dirs "**/bin" `
  --skip-dirs obj `
  --skip-dirs "**/obj" `
  --skip-dirs .vs `
  --skip-dirs .idea `
  --skip-dirs TestResults `
  --skip-dirs "**/TestResults" `
  --skip-dirs coverage `
  --skip-dirs CodeCoverage `
  --skip-dirs StrykerOutput `
  --skip-dirs .dotnet `
  --skip-dirs .dotnet-home `
  --skip-dirs .nuget `
  --skip-dirs artifacts `
  --skip-dirs infra/nginx/certs `
  .
```

O scan de configuracao usa tambem
`infra/terraform/environments/dev/validation.tfvars`, que contem apenas valores
nao sensiveis para analise estatica (`project_id` e `region`). Esse arquivo nao
substitui `terraform.tfvars` local e nao deve receber segredos.

Na pratica, isso cobre:

- Dockerfiles e configuracoes de containers;
- Terraform e outras configuracoes IaC reconhecidas pelo Trivy;
- misconfigurations com severidade `HIGH` ou `CRITICAL`;
- secrets detectados pelo scanner do Trivy;
- vulnerabilidades em manifests e arquivos de lock reconhecidos pelo scanner de filesystem.

As validacoes nao executam build de imagem Docker, nao exigem credenciais cloud e nao executam `terraform plan`, `terraform apply` ou `terraform destroy`.

## Instalacao local no Windows

Com Chocolatey:

```powershell
choco install trivy -y
```

Com Winget:

```powershell
winget install AquaSecurity.Trivy
```

Confirme a instalacao:

```powershell
trivy --version
```

## Hook local

O repositorio versiona hooks em `.githooks/`. Em geral, o build de `src/balance/BalanceService.Api/BalanceService.Api.csproj` configura automaticamente:

```bash
git config core.hooksPath .githooks
```

Tambem e possivel configurar manualmente:

```bash
git config core.hooksPath .githooks
```

No Linux/macOS, se o hook nao estiver executavel:

```bash
chmod +x .githooks/pre-push
```

No Windows, execute os hooks com Git Bash ou outro shell POSIX compativel.

O `pre-push` nao executa Trivy automaticamente. Para feedback local antecipado, execute os comandos manuais desta pagina. A ausencia do Trivy local nao impede o envio da branch, porque o Pull Request continua protegido pelo workflow de infraestrutura quando os filtros de caminho se aplicam.

## Execucao manual

Na raiz do repositorio:

```powershell
trivy config `
  --severity HIGH,CRITICAL `
  --tf-vars infra/terraform/environments/dev/validation.tfvars `
  --skip-dirs node_modules `
  --skip-dirs .git `
  --skip-dirs dist `
  --skip-dirs .terraform `
  --skip-dirs bin `
  --skip-dirs "**/bin" `
  --skip-dirs obj `
  --skip-dirs "**/obj" `
  --skip-dirs .vs `
  --skip-dirs .idea `
  --skip-dirs TestResults `
  --skip-dirs "**/TestResults" `
  --skip-dirs coverage `
  --skip-dirs CodeCoverage `
  --skip-dirs StrykerOutput `
  --skip-dirs .dotnet `
  --skip-dirs .dotnet-home `
  --skip-dirs .nuget `
  --skip-dirs artifacts `
  --skip-dirs infra/nginx/certs `
  .
trivy fs `
  --scanners vuln,secret `
  --severity HIGH,CRITICAL `
  --tf-vars infra/terraform/environments/dev/validation.tfvars `
  --skip-dirs node_modules `
  --skip-dirs .git `
  --skip-dirs dist `
  --skip-dirs .terraform `
  --skip-dirs bin `
  --skip-dirs "**/bin" `
  --skip-dirs obj `
  --skip-dirs "**/obj" `
  --skip-dirs .vs `
  --skip-dirs .idea `
  --skip-dirs TestResults `
  --skip-dirs "**/TestResults" `
  --skip-dirs coverage `
  --skip-dirs CodeCoverage `
  --skip-dirs StrykerOutput `
  --skip-dirs .dotnet `
  --skip-dirs .dotnet-home `
  --skip-dirs .nuget `
  --skip-dirs artifacts `
  --skip-dirs infra/nginx/certs `
  .
```

Para reproduzir o comportamento bloqueante usado pelo hook:

```powershell
trivy config `
  --severity HIGH,CRITICAL `
  --exit-code 1 `
  --tf-vars infra/terraform/environments/dev/validation.tfvars `
  --skip-dirs node_modules `
  --skip-dirs .git `
  --skip-dirs dist `
  --skip-dirs .terraform `
  --skip-dirs bin `
  --skip-dirs "**/bin" `
  --skip-dirs obj `
  --skip-dirs "**/obj" `
  --skip-dirs .vs `
  --skip-dirs .idea `
  --skip-dirs TestResults `
  --skip-dirs "**/TestResults" `
  --skip-dirs coverage `
  --skip-dirs CodeCoverage `
  --skip-dirs StrykerOutput `
  --skip-dirs .dotnet `
  --skip-dirs .dotnet-home `
  --skip-dirs .nuget `
  --skip-dirs artifacts `
  --skip-dirs infra/nginx/certs `
  .
trivy fs `
  --scanners vuln,secret `
  --severity HIGH,CRITICAL `
  --exit-code 1 `
  --tf-vars infra/terraform/environments/dev/validation.tfvars `
  --skip-dirs node_modules `
  --skip-dirs .git `
  --skip-dirs dist `
  --skip-dirs .terraform `
  --skip-dirs bin `
  --skip-dirs "**/bin" `
  --skip-dirs obj `
  --skip-dirs "**/obj" `
  --skip-dirs .vs `
  --skip-dirs .idea `
  --skip-dirs TestResults `
  --skip-dirs "**/TestResults" `
  --skip-dirs coverage `
  --skip-dirs CodeCoverage `
  --skip-dirs StrykerOutput `
  --skip-dirs .dotnet `
  --skip-dirs .dotnet-home `
  --skip-dirs .nuget `
  --skip-dirs artifacts `
  --skip-dirs infra/nginx/certs `
  .
```

Os scans continuam analisando arquivos versionados relevantes como `Directory.Packages.props`, `.csproj`, Dockerfiles, Compose, Terraform e configuracoes. Os `skip-dirs` cobrem apenas diretorios gerados, dependencias locais, caches locais e certificados locais do Nginx que nao devem ser versionados, evitando falsos positivos fora do codigo do projeto.

## CI

O workflow `infrastructure-security` roda Trivy em pull requests e em pushes para `main` quando ha mudancas em Terraform, Dockerfiles, Compose, na composite action do Trivy ou no proprio workflow.

No CI, a validacao executa independentemente da instalacao local do desenvolvedor. Os scans sao bloqueantes para severidades `HIGH` e `CRITICAL`, porque nao dependem de credenciais cloud nem alteram infraestrutura real.

O scan local serve apenas como feedback antecipado manual antes do PR; o CI continua sendo a linha de defesa obrigatoria. Por isso, a ausencia local do Trivy nunca bloqueia o `git push`, mas a mesma classe de achado bloqueia o pull request quando detectada pelo workflow.

O workflow nao instala Terraform ou TFLint. Validacao Terraform fica no workflow independente `terraform-validation`, que roda apenas para mudancas em Terraform, scripts de validacao Terraform ou no proprio workflow.

Se os argumentos comuns do Trivy mudarem no CI, atualize a composite action `.github/actions/trivy-repository-scan` e confira se os exemplos deste documento continuam equivalentes.
