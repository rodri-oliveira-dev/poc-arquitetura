# Atualizacao automatizada de dependencias

O repositorio usa Dependabot como camada preventiva da baseline de supply chain. Ele complementa CodeQL, Dependency Review, NuGet Audit, Trivy e os demais gates existentes; nao substitui nenhum deles.

## Ecossistemas monitorados

A configuracao versionada em `.github/dependabot.yml` cobre:

- NuGet na raiz, incluindo o Central Package Management do repositorio;
- GitHub Actions em `.github/workflows` e composite actions elegiveis;
- a imagem base do Nginx em `infra/nginx`;
- providers e modules Terraform nos environments e modulos reutilizaveis.

As verificacoes executam semanalmente, em horarios escalonados no timezone `America/Sao_Paulo`, para evitar uma rajada unica de pull requests.

## Politica de atualizacao

Atualizacoes `minor` e `patch` podem ser agrupadas por ecossistema para reduzir ruido. Atualizacoes `major` permanecem fora desses grupos e devem ser avaliadas individualmente, porque podem alterar contratos, compatibilidade, comportamento de runtime ou requisitos de infraestrutura.

PRs criados pelo Dependabot devem passar pelos mesmos gates aplicaveis a qualquer outra mudanca. Nao relaxe CI, CodeQL, Dependency Review, Trivy, validacao Terraform ou controles de container apenas para permitir uma atualizacao automatizada.

## Terraform e lock files

Roots Terraform que versionam `.terraform.lock.hcl` devem manter o lock file coerente com qualquer atualizacao de provider. Modulos reutilizaveis podem nao possuir lock file proprio; nesse caso, a validacao ocorre pelos roots e testes que os consomem.

Dependabot apenas propoe a mudanca. `terraform apply` e `terraform destroy` nao fazem parte da validacao automatica de pull requests e continuam fora do fluxo de atualizacao de dependencias.

## Segredos e credenciais

A manutencao automatizada deve permanecer credential-free sempre que possivel. Nenhum token GCP, segredo de aplicacao, registry privado ou credencial persistente deve ser adicionado ao `dependabot.yml` para suportar os ecossistemas publicos usados por esta POC.

## Revisao de um PR do Dependabot

Antes do merge, valide o impacto da versao proposta, os changelogs relevantes e os checks produzidos pelo repositorio. Mudancas que exigirem migracao, alterarem defaults ou ampliarem permissoes devem receber o mesmo nivel de revisao de uma mudanca manual equivalente.
