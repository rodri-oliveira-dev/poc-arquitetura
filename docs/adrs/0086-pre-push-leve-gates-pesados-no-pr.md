# ADR-0086: Pre-push leve com gates pesados no Pull Request

## Status
Parcialmente substituido pela [ADR-0106](./0106-ci-principal-contextual-pull-requests-main.md) para a organizacao do CI de Pull Requests

## Data
2026-06-13

## Contexto
O repositorio ja possui hooks locais, workflows de Pull Request, cobertura, SonarQube Cloud, Trivy, Terraform validate e testes de integracao com Testcontainers/PostgreSQL.

Com a evolucao da suite, o `pre-push` local passou a ter custo alto e podia falhar por dependencias ambientais que nao sao necessarias para feedback rapido, especialmente Docker-compatible API indisponivel para testes com Testcontainers.

## Problema
O `pre-push` estava se aproximando de um pipeline completo: executava validacoes de Terraform e Trivy antes de considerar o diff e rodava a suite de testes sem filtro. Isso aumentava o tempo de push, duplicava checks ja cobertos pelo GitHub Actions e podia bloquear o desenvolvedor por Docker desligado, mesmo quando o objetivo local era apenas detectar erros obvios antes de enviar a branch.

## Decisao
Manter o `pre-push` como validacao local leve:

- calcular arquivos alterados antes de executar validacoes;
- executar `terraform fmt -check` apenas quando houver alteracoes em `*.tf` ou `*.tfvars`;
- executar restore, `dotnet format whitespace --verify-no-changes` nos arquivos `.cs` alterados, build e testes sem cobertura apenas quando houver alteracoes .NET impactantes;
- pular a formatacao .NET local quando o diff tiver mais de 30 arquivos `.cs`, mantendo build, testes rapidos e gates de Pull Request;
- filtrar os testes locais com `Category!=Integration&Category!=Container&Category!=Contract`;
- medir a duracao aproximada de cada etapa local executada;
- permitir validacao completa explicita com `FULL_TESTS=true git push`, reaproveitando `./test.sh` e o gate oficial de cobertura;
- nao executar Trivy, cobertura, ReportGenerator, SonarQube, Docker build, scan de imagem, Terraform validate completo, testes de integracao ou Testcontainers no `pre-push`.

O Pull Request permanece como gate forte:

- `pr-build-and-test` executa restore, build e testes completos sem filtro quando ha mudanca impactante;
- `main-dotnet-ci` executa restore, auditoria NuGet, SonarQube Cloud, build, testes com cobertura, ReportGenerator e gate de 85%;
- no momento da decisao, `infra-security-and-terraform-validation` executava Trivy repository scan, Terraform `fmt`, `init -backend=false`, `validate` e TFLint para mudancas de infraestrutura, Dockerfile e Compose.

Nota de manutencao: em 2026-07-14, as responsabilidades de CI foram separadas. `infrastructure-security` passou a executar Trivy para Dockerfiles, Compose, Terraform, secrets e filesystem, enquanto `terraform-validation` passou a executar apenas Terraform `fmt`, `init -backend=false`, `validate` e TFLint para mudancas relacionadas a Terraform.

## Consequencias

### Beneficios
- Reduz tempo de feedback no `git push`.
- O push local nao falha por Docker desligado.
- Mantem testes unitarios, build e formatacao dos arquivos alterados como defesa rapida antes do PR.
- Evita que branches grandes gastem varios minutos apenas na formatacao local.
- Torna gargalos locais visiveis por logs de duracao simples.
- Permite que um desenvolvedor execute a validacao completa no proprio push quando quiser feedback maximo antes do PR.
- Remove duplicidade local de checks pesados ja cobertos no GitHub Actions.
- Mantem seguranca e qualidade no PR, onde o runner possui ambiente controlado.

### Trade-offs / custos
- Alguns problemas passam a ser descobertos no PR em vez de no push local.
- Em branches com mais de 30 arquivos C#, problemas de formatacao podem ser descobertos no PR ou por validacao manual.
- Desenvolvedores que quiserem feedback completo antes do PR precisam executar `./test.sh`, `./test.ps1`, Trivy ou `scripts/quality/terraform/validate.sh` manualmente.
- A categorizacao de testes precisa ser mantida sempre que novos testes de integracao, contrato ou container forem criados.

### Riscos
- Um teste dependente de Docker sem categoria adequada poderia voltar a rodar no `pre-push`. Para reduzir o risco, projetos `*.IntegrationTests` recebem categoria `Integration`, e testes PostgreSQL via Testcontainers recebem tambem `Container`.
- Se branch protection nao exigir os checks de PR relevantes, a seguranca efetiva fica dependente de configuracao externa do GitHub. A documentacao de Pull Request continua registrando os required checks esperados.

## Alternativas consideradas

1. **Manter pre-push completo**
   - Rejeitado pelo custo alto e por bloquear desenvolvedores por dependencias ambientais locais.

2. **Remover todos os testes do pre-push**
   - Rejeitado porque build e testes unitarios ainda oferecem feedback rapido e barato para erros obvios.

3. **Executar Testcontainers condicionalmente quando Docker estiver ligado**
   - Rejeitado como comportamento padrao porque torna o hook variavel por maquina. Testes de container devem ser execucao manual local ou gate de PR.

4. **Executar Terraform validate completo localmente apenas em mudancas `.tf`**
   - Rejeitado para o hook padrao porque exige Terraform, providers, TFLint e inicializacao. O PR ja cobre essa validacao em runner controlado.

## Impacto no fluxo local
`git push` executa apenas validacoes locais leves proporcionais ao diff. Docker desligado nao impede o push por causa de Testcontainers. Para cobertura, Trivy, Terraform validate completo e testes de integracao/container, use os comandos manuais documentados ou abra o Pull Request. Se quiser executar a validacao completa oficial antes do envio, use `FULL_TESTS=true git push`.

## Impacto no CI
Nao ha reducao intencional de seguranca no CI. Os workflows existentes continuam sendo a linha de defesa para testes completos, cobertura, SonarQube, Trivy e Terraform validate.
