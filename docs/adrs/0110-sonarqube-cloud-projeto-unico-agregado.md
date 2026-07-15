# ADR-0110 - SonarQube Cloud com projeto unico agregado

Status: Aceito

## Contexto

A ADR-0106 registrou o CI principal com dois contextos SonarQube Cloud: aggregate e Shared. Na pratica, o projeto oficial mantido no SonarQube Cloud e unico:

- Project Key: `rodri-oliveira-dev_poc-arquitetura`
- Project Name: `poc-arquitetura`
- Organization: `rodri-oliveira-dev`

Manter um segundo projeto para Shared cria custo operacional e ambiguidade. Reutilizar o mesmo project key agregado para uma segunda analise baseada em `PocArquitetura.Shared.slnx` tambem seria incorreto, porque uma analise parcial enviada depois da agregada poderia substituir ou distorcer o resultado do repositorio completo.

## Decisao

O workflow `.github/workflows/dotnet.yml` deve enviar analise SonarQube Cloud somente pelo contexto `aggregate`, usando `./PocArquitetura.slnx` e o project key `rodri-oliveira-dev_poc-arquitetura`.

O contexto `shared` continua existindo no CI para validacoes locais especificas:

- restore de `./PocArquitetura.Shared.slnx`;
- auditoria de vulnerabilidades NuGet;
- build;
- testes;
- geracao de cobertura Cobertura e OpenCover;
- ReportGenerator;
- gate local de cobertura de 80%;
- resumo no GitHub Actions.

Shared nao executa `dotnet-sonarscanner begin`, `dotnet-sonarscanner end`, espera de Quality Gate remoto, consulta da API do SonarQube Cloud ou relatorio Sonar proprio.

Alteracoes em `src/Shared/**`, `tests/Shared/**` ou `PocArquitetura.Shared.slnx` devem acionar:

```text
run_aggregate=true
run_shared=true
```

Assim, a analise Sonar completa continua sendo enviada pelo unico projeto oficial e o gate local especifico de Shared permanece ativo.

## Consequencias

- O SonarQube Cloud passa a representar o repositorio completo apenas pelo projeto `rodri-oliveira-dev_poc-arquitetura`.
- Nao devem ser criados projetos Sonar adicionais por bounded context, por solution ou para Shared sem nova ADR.
- O secret `SONAR_TOKEN` continua obrigatorio quando o aggregate executa Sonar e continua desnecessario em PRs documentais sem impacto .NET.
- O artifact consolidado `test-results-coverage-and-sonarqube` mantem testes, cobertura, auditoria NuGet e relatorio Sonar apenas em `artifacts/sonarqube/aggregate`.
- A ADR-0106 permanece como historico do modelo anterior, substituida por esta decisao na parte de estrategia SonarQube Cloud.
