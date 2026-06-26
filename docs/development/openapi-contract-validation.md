# Validacao dos contratos OpenAPI

Este guia descreve como os contratos HTTP versionados sao gerados, lintados, comparados contra drift e comparados contra a `main` para detectar breaking changes em pull requests.

## Contratos versionados

Os contratos ficam em:

- `docs/openapi/ledger.v1.json`
- `docs/openapi/balance.v1.json`
- `docs/openapi/transfer.v1.json`
- `docs/openapi/identity.v1.json`

Eles sao gerados a partir dos assemblies Release das APIs, usando o documento Swagger `v1`. A geracao nao sobe Docker, nao chama endpoints HTTP e usa valores sinteticos de ambiente apenas para inicializar os hosts em modo OpenAPI.

Fluxo local:

```bash
dotnet build ./LedgerService.slnx --configuration Release --no-restore
./scripts/contracts/openapi/generate.sh
```

No Windows, tambem existe `./scripts/contracts/openapi/generate.ps1`.

## Drift versus breaking change

Drift e a diferenca entre o contrato gerado a partir do codigo atual e os arquivos versionados em `docs/openapi/`. O drift indica que o codigo e o contrato commitado nao representam a mesma coisa. O workflow falha quando `git diff -- docs/openapi` encontra alteracoes depois da geracao.

Breaking change e uma diferenca entre o contrato publico da branch atual e o contrato versionado na `main` que pode quebrar clientes existentes. Uma mudanca pode nao ter drift e ainda assim ser breaking, por exemplo quando o contrato gerado foi atualizado corretamente no PR, mas removeu um endpoint existente.

## Lint com Redocly

O lint usa o Redocly CLI versionado em `package.json` e `package-lock.json`.

```bash
npm ci
npm run openapi:lint
```

As APIs lintadas sao declaradas em `redocly.yaml`. Algumas regras continuam como warning para evitar transformar todos os avisos em bloqueios. O objetivo do lint e manter qualidade estrutural e legibilidade do contrato sem substituir testes ou revisao de compatibilidade.

Algumas regras do preset recomendado ficam desabilitadas por decisao explicita da POC:

- `info-license`: o repositorio nao declara uma licenca publica; o contrato nao deve inventar uma.
- `no-server-example.com`: os contratos versionados documentam os endpoints locais diretos usados pelos scripts e pela stack de desenvolvimento.
- `no-ambiguous-paths`: o Ledger usa constraints `guid` em rotas ASP.NET Core; essas constraints removem a ambiguidade em runtime, mas nao sao expressaveis no path OpenAPI.
- `operation-4xx-response`: endpoints de negocio devem declarar respostas 4xx relevantes; `/health` e `/ready` sao endpoints operacionais publicos e leves, sem 4xx artificial apenas para satisfazer lint.

## Diff contra a main

Em pull requests para `main`, o workflow `openapi-contract-validation` executa estes passos:

1. Faz checkout da branch do PR.
2. Restaura ferramentas .NET e dependencias Node.
3. Compila a solution em Release.
4. Gera os contratos atuais em `docs/openapi/`.
5. Executa o lint com Redocly.
6. Busca a referencia base da `main`.
7. Extrai os contratos em `docs/openapi/*.v1.json` da `main` para `.openapi-main/`, quando os contratos ja existem na branch base.
8. Executa `npm run openapi:diff` apenas quando todos os contratos esperados existem na `main`.
9. Valida drift nos contratos gerados.

O script `scripts/contracts/openapi/check-breaking-changes.sh` compara os contratos da `main` com os contratos gerados na branch usando `oasdiff breaking --fail-on ERR`. Esse modo falha apenas para mudancas classificadas como erro, que representam breaking changes mais claros. Warnings continuam visiveis na saida, mas nao bloqueiam a etapa inicial.

```bash
./scripts/contracts/openapi/check-breaking-changes.sh
```

Quando a `main` ainda nao possui um dos contratos versionados esperados, como no PR que introduz o baseline inicial, o workflow registra um notice e pula a comparacao de breaking changes. Se a geracao tambem produzir drift em `docs/openapi/`, o workflow registra warning e summary, mas nao bloqueia esse primeiro baseline. A geracao e o lint continuam obrigatorios. Depois que o baseline entrar na `main`, pull requests seguintes voltam a comparar contra a base normalmente e drift volta a ser bloqueante.

Em push na `main`, o workflow nao compara a branch contra ela mesma. Ele gera, linta e valida drift. Em `workflow_dispatch`, o comportamento tambem e seguro: quando nao ha pull request, a comparacao contra `main` e ignorada e o summary registra essa decisao.

## Mudancas que tendem a ser breaking

Exemplos comuns:

- remover endpoint, metodo HTTP, status code de sucesso ou response body existente;
- renomear path, parametro, propriedade de request ou propriedade de response;
- tornar obrigatorio um parametro ou campo antes opcional;
- alterar tipo, formato, nulabilidade ou enum de campos usados por clientes;
- remover media type, header, scope, esquema de seguranca ou contrato de erro esperado;
- estreitar regras documentadas de entrada de forma incompativel com clientes atuais.

A ferramenta nao substitui revisao humana. Mudancas comportamentais que nao aparecem no OpenAPI, como semantica de autorizacao por merchant, idempotencia, ordenacao, consistencia ou efeitos colaterais, ainda precisam ser avaliadas no PR.

## Quebra intencional

Quando uma quebra for intencional, trate como mudanca de contrato publico:

1. Explique a motivacao no PR.
2. Atualize a documentacao de API afetada, como `docs/development/ledger-api.md` ou `docs/development/balance-api.md`.
3. Atualize documentacao operacional ou arquitetural quando o fluxo mudar.
4. Crie ou atualize ADR quando houver decisao arquitetural, estrategia de compatibilidade, versionamento, seguranca, persistencia, mensageria ou comportamento relevante.
5. Combine a estrategia de migracao com os consumidores antes de liberar a mudanca.

Nao edite manualmente `docs/openapi/*.json` apenas para passar no diff. O contrato deve continuar sendo gerado a partir do codigo.
