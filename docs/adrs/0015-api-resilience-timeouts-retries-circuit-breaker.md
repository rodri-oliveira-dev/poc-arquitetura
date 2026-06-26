# ADR-0015: (Ponto de melhoria) Resiliência de chamadas externas: timeouts, retries e circuit breaker

## Status
Proposto

## Data
2026-02-18

## Contexto
Mesmo em uma PoC, existem chamadas externas e dependências que podem falhar:

- APIs de negócio consultam JWKS do `Auth.Api` (ADR-0004) para validar tokens.
- Serviços dependem de Postgres e Kafka (com boot e readiness variáveis).

O repositório já toma algumas decisões corretas:

- validação offline do JWT reduz dependência do Auth por request;
- consumer/publisher têm *retry/backoff* “na unha” (via loops e delays);
- migrations não rodam no startup (ADR-0011/README), reduzindo efeitos colaterais.

Porém, falta uma política explícita e padronizada de resiliência para chamadas HTTP e para inicialização/conectividade.

## Decisão
Padronizar resiliência em dois níveis:

1) **HTTP client resiliente (JWKS / outras integrações futuras)**
   - Adotar `HttpClientFactory` com:
     - **timeouts curtos** e explícitos;
     - **retry com backoff** e jitter (apenas para falhas transitórias);
     - **circuit breaker** para evitar tempestade de retries quando o destino estiver indisponível.
   - Logar falhas de refresh do JWKS com `CorrelationId` quando aplicável.

2) **Startup/readiness alinhados**
   - Readiness (`/ready`, ADR-0014) deve refletir:
     - conectividade com DB;
     - capacidade mínima de produzir/consumir Kafka (quando for crítico para o serviço).
   - A API não deve “cair” em falhas transitórias, mas também não deve aceitar tráfego sem estar pronta.

## Consequências

### Benefícios
- Reduz falhas em cascata e melhora estabilidade em ambientes instáveis.
- Padroniza comportamento entre serviços.
- Dá insumo para observabilidade (métricas de retry/breaker e logs consistentes).

### Trade-offs / custos
- Exige dependências e configuração (ex.: Polly / Microsoft.Extensions.Resilience).
- Risco de “mascarar” problemas se timeouts e retries forem agressivos.

## Alternativas consideradas

1) **Sem políticas padronizadas (cada serviço resolve como quiser)**
   - Prós: mais rápido.
   - Contras: drift, bugs sutis e comportamentos inconsistentes.

2) **Apenas aumentar timeouts**
   - Prós: fácil.
   - Contras: piora latência e aumenta fila de threads sob falha.

## Historico de implementacao

Em 2026-06-25, a politica compartilhada em `HttpResilienceDefaults` passou a emitir logs e metricas de resiliencia HTTP com `System.Diagnostics.Metrics`.

Sinais emitidos:

- retries por cliente;
- timeouts por cliente;
- circuit breaker open, half-open e closed;
- chamadas rejeitadas por circuito aberto;
- duracao das chamadas HTTP resilientes.

Tags de baixa cardinalidade:

- `client`;
- `dependency`;
- `operation`, quando disponivel;
- `outcome`;
- `exception_type`, quando aplicavel.

Clientes cobertos:

- `JWKS`, usado pelas APIs para validacao JWT;
- `Keycloak`, usado pelo token provider client credentials do `TransferService.Worker`;
- `Ledger`, usado pelo `TransferService.Worker` para chamar o `LedgerService.Api`.

Os logs preservam o escopo de correlacao existente quando disponivel e nao registram segredo, token, client secret, URL completa nem payload sensivel.

## Próximos passos (não implementados)

- TODO: escolher abordagem: Polly vs `Microsoft.Extensions.Resilience` (dependendo do target do .NET 10).
