# Validacao de seguranca com Trivy

O repositorio usa Trivy para feedback antecipado sobre configuracoes de infraestrutura, Dockerfiles, secrets e vulnerabilidades detectaveis no filesystem versionado.

## O que e validado

O hook local `.githooks/pre-push` e o workflow `.github/workflows/terraform-validation.yml` executam:

```powershell
trivy config --severity HIGH,CRITICAL .
trivy fs --scanners vuln,secret,misconfig --severity HIGH,CRITICAL .
```

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

O repositorio versiona hooks em `.githooks/`. Em geral, o build de `src/BalanceService.Api/BalanceService.Api.csproj` configura automaticamente:

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

Quando o Trivy esta instalado, o `pre-push` bloqueia o push se encontrar achados `HIGH` ou `CRITICAL`. Quando o Trivy nao esta instalado, o hook mostra um aviso amigavel e permite o push; a ausencia do Trivy local nao impede o envio da branch.

## Execucao manual

Na raiz do repositorio:

```powershell
trivy config --severity HIGH,CRITICAL .
trivy fs --scanners vuln,secret,misconfig --severity HIGH,CRITICAL .
```

Para reproduzir o comportamento bloqueante usado pelo hook:

```powershell
trivy config --severity HIGH,CRITICAL --exit-code 1 .
trivy fs --scanners vuln,secret,misconfig --severity HIGH,CRITICAL --exit-code 1 .
```

## CI

O workflow `terraform-validation` roda Trivy em pull requests e em pushes para `main` quando ha mudancas em Terraform, Dockerfiles, Compose ou no proprio workflow.

No CI, a validacao executa independentemente da instalacao local do desenvolvedor. Os scans sao bloqueantes para severidades `HIGH` e `CRITICAL`, porque nao dependem de credenciais cloud nem alteram infraestrutura real.

O hook local serve apenas como feedback antecipado antes do PR; o CI continua sendo a linha de defesa obrigatoria. Por isso, a ausencia local do Trivy nunca bloqueia o `git push`, mas a mesma classe de achado bloqueia o pull request quando detectada pelo workflow.

O workflow tambem executa Terraform e TFLint por meio de `scripts/validate-terraform.sh`. Essa etapa instala as ferramentas no runner, nao usa credenciais cloud e nao executa `terraform plan`, `terraform apply` ou `terraform destroy`.
