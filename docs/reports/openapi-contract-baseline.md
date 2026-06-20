# Baseline dos contratos OpenAPI

## Contratos gerados

- `docs/openapi/ledger.v1.json`
- `docs/openapi/balance.v1.json`

Os contratos foram gerados com `scripts/contracts/openapi/generate.sh` a partir dos assemblies Release de `LedgerService.Api` e `BalanceService.Api`, usando o documento Swagger `v1`.

## Resumo por API

| API | Arquivo | Paths | Operacoes |
| --- | --- | ---: | ---: |
| LedgerService.Api | `docs/openapi/ledger.v1.json` | 9 | 9 |
| BalanceService.Api | `docs/openapi/balance.v1.json` | 4 | 4 |

Paths gerados para Ledger:

- `/api/v1/lancamentos`
- `/api/v1/lancamentos/{lancamentoId}/estornos`
- `/api/v1/lancamentos/reprocessar`
- `/api/v1/lancamentos/estornos/{estornoId}`
- `/api/v1/lancamentos/reprocessamentos/{reprocessamentoId}`
- `/health`
- `/ready`
- `/api/v1/outbox/dead-letters`
- `/api/v1/outbox/dead-letters/{id}/requeue`

Paths gerados para Balance:

- `/health`
- `/ready`
- `/api/v1/consolidados/diario/{date}`
- `/api/v1/consolidados/periodo`

## Warnings observados

- A geracao nao incluiu bloco `servers`, evitando dependencia de URL local instavel.
- Nao foram encontrados secrets, connection strings reais, hosts locais ou credenciais nos contratos.
- Ha descricoes de autenticacao com texto sobre `token` e `JWT Bearer`; isso descreve o mecanismo de seguranca e nao expoe valor sensivel.
- Ha exemplos estaticos com datas de negocio em 2026. Eles permaneceram deterministicos entre execucoes e nao representam timestamp de geracao.
- Endpoints de health e readiness aparecem com tags derivadas do assembly, como `LedgerService.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` e `BalanceService.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`.
- Algumas operacoes nao possuem `operationId`: 7 em Ledger e 2 em Balance.
- Nao foram encontradas responses `default` nem summaries vazios.
- O build Release executado antes da geracao terminou sem erros, mas manteve avisos de analisadores ja existentes no repositorio.

## Ajustes recomendados

- Definir tags explicitas para health/readiness para evitar nomes de assembly no contrato publico.
- Avaliar `operationId` explicito para todos os endpoints antes de automatizar clientes ou comparacao de breaking changes.
- Revisar exemplos e descricoes de seguranca em uma etapa futura, mantendo dados sinteticos e estaticos.
- Criar validacao automatizada de contrato e breaking changes em etapa separada, conforme planejado.

## Ambiente e determinismo

A geracao depende de:

- .NET SDK compativel com `global.json`.
- Ferramentas restauradas por `dotnet tool restore`, incluindo `swashbuckle.aspnetcore.cli`.
- Build previo dos assemblies em `Release` para `net10.0`.
- Bash para executar `scripts/contracts/openapi/generate.sh`. Neste Windows, o comando `bash ./scripts/contracts/openapi/generate.sh` via WindowsApps/WSL falhou por ausencia de `/bin/bash`; a execucao validada usou `C:/Program Files/Git/bin/bash.exe`.

O script define valores sinteticos para ambiente OpenAPI, JWT e connection string apenas para inicializar os assemblies durante a geracao. Esses valores nao aparecem como secrets nos contratos gerados.

Foram executadas duas geracoes consecutivas. Os hashes SHA-256 permaneceram iguais e `git diff -- docs/openapi` nao mostrou alteracoes apos a segunda execucao.
