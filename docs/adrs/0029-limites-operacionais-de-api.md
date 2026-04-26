# ADR-0029: Limites operacionais de API

## Status
Aceito

## Data
2026-04-26

## Contexto
`LedgerService.Api` e `BalanceService.Api` ja possuiam rate limit fixo nas rotas de negocio, mas sem configuracao externa para esses valores e sem limite explicito de tamanho de body HTTP. O `BalanceService.Api` tambem permitia consultas por periodo sem limite maximo de intervalo.

Sem esses limites, consumidores podem provocar consumo excessivo de memoria, CPU ou banco de dados com payloads grandes ou consultas de periodo muito amplo. Como os servicos sao APIs de borda da POC, esses controles devem ficar explicitos no contrato HTTP e configuraveis por ambiente.

## Decisão
Padronizar limites operacionais configuraveis nas APIs:

- `ApiLimits:MaxRequestBodySizeBytes` define o tamanho maximo de body HTTP em `LedgerService.Api` e `BalanceService.Api`;
- o mesmo limite e aplicado no Kestrel e em middleware de pipeline para retornar `413 Payload Too Large` quando `Content-Length` excede o valor configurado;
- `ApiLimits:MaxBalancePeriodDays` define o intervalo maximo inclusivo de `GET /v1/consolidados/periodo` no `BalanceService.Api`;
- intervalos acima do limite retornam `400 Bad Request` com resposta de validacao;
- `ApiLimits:RateLimitPermitLimit`, `ApiLimits:RateLimitWindowSeconds` e `ApiLimits:RateLimitQueueLimit` externalizam o rate limit fixo ja existente nas rotas de negocio;
- os defaults versionados sao 1 MiB para body, 31 dias para periodo do Balance e 100 requests por 60 segundos com fila de 10 para rate limit;
- ambientes diferentes podem ajustar esses valores por `appsettings.*.json`, variaveis de ambiente ou outro provider de configuracao do .NET.

Arquivos afetados:

- `src/LedgerService.Api`
- `src/BalanceService.Api`
- `tests/LedgerService.IntegrationTests`
- `tests/BalanceService.IntegrationTests`
- `README.md`

## Consequências

### Benefícios
- Reduz risco de consumo excessivo de recursos por payloads grandes.
- Limita consultas amplas no BalanceService, evitando leituras desnecessariamente custosas.
- Mantem os limites operacionais ajustaveis por ambiente sem recompilacao.
- Alinha codigo, testes, Swagger e README em torno do mesmo contrato.

### Trade-offs / custos
- Ambientes com necessidade legitima de payload ou periodo maior precisam ajustar configuracao explicitamente.
- O middleware rejeita antecipadamente apenas quando `Content-Length` esta presente; Kestrel permanece como protecao do host real para o limite de body durante leitura.
- Rate limit continua usando janela fixa simples, adequada para a POC, mas menos flexivel que politicas por usuario, merchant ou token.

## Alternativas consideradas

1) **Manter apenas rate limit**
   - Pros: nenhuma mudanca de contrato.
   - Contras: nao limita payload e nao protege consultas de periodo amplo.

2) **Definir limites hard-coded**
   - Pros: implementacao menor.
   - Contras: dificulta ajuste por ambiente e contraria a necessidade operacional.

3) **Aplicar limite de periodo na camada de dominio**
   - Pros: centralizaria a regra.
   - Contras: o limite e operacional/contratual da API de leitura, nao uma regra de dominio persistente; manter na borda evita acoplamento indevido.
