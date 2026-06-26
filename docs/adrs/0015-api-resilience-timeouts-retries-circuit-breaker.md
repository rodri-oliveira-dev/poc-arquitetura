# ADR-0015: (Ponto de melhoria) Resiliência de chamadas externas: timeouts, retries e circuit breaker

## Status
Parcialmente implementado

## Data
2026-02-18

## Contexto
Mesmo em uma PoC, existem chamadas externas e dependências que podem falhar:

- APIs de negócio consultam JWKS do provedor de identidade configurado para validar tokens offline.
- Serviços dependem de Postgres e Kafka (com boot e readiness variáveis).

O repositório já toma algumas decisões corretas:

- validação offline do JWT reduz dependência do Auth por request;
- consumer/publisher têm *retry/backoff* “na unha” (via loops e delays);
- migrations não rodam no startup (ADR-0011/README), reduzindo efeitos colaterais.

Quando a ADR foi criada, faltava uma política explícita e padronizada de resiliência para chamadas HTTP e para inicialização/conectividade.

## Decisão
Padronizar resiliência em dois níveis:

1) **HTTP client resiliente (JWKS / outras integrações futuras)**
   - Adotar `HttpClientFactory` com:
     - **timeouts curtos** e explícitos;
     - **retry com delay configurável** apenas para falhas transitórias;
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

## Implementação atual

Em 2026-06-25, a implementação de HTTP resiliente foi consolidada no projeto compartilhado `src/Shared/HttpResilienceDefaults`.

A abordagem escolhida foi `Microsoft.Extensions.Http.Resilience`, usando `AddStandardResilienceHandler()` sobre Polly v8. O repositório não usa policies Polly manuais nos clients HTTP atuais. A política compartilhada configura:

- timeout total da chamada;
- timeout por tentativa;
- retry;
- circuit breaker;
- logs estruturados;
- métricas customizadas por `System.Diagnostics.Metrics`.

O backoff efetivo de retry é constante porque o wrapper configura `Retry.Delay` e não sobrescreve `BackoffType` nem `UseJitter` do Polly. Não há jitter configurado explicitamente no código atual.

### Clientes cobertos

| Cliente | Processo | Uso | Método inseguro com retry |
| --- | --- | --- | --- |
| `JWKS` | APIs que usam `AddConfiguredApiJwtBearerAuthentication` | Busca de chaves públicas em `Jwt:JwksUrl` para validação JWT offline. | Não. `GET` é coberto; métodos inseguros são desabilitados pela política compartilhada. |
| `Keycloak` | `TransferService.Worker` | Token endpoint usado por `ClientCredentialsLedgerAccessTokenProvider`. | Sim. `RetryUnsafeHttpMethods=true` porque o token endpoint usa `POST`. |
| `Ledger` | `TransferService.Worker` | Chamadas service-to-service para `LedgerService.Api` na Saga de transferência. | Sim. `RetryUnsafeHttpMethods=true` porque as etapas usam `Idempotency-Key` determinístico. |

`TransferService.Api` também usa o componente compartilhado de autenticação JWT; portanto, quando configurado com `Jwt:*`, seu fetch de JWKS passa pelo mesmo client `JWKS`.

### Defaults efetivos

Defaults globais de `HttpClientResilienceOptions`, usados quando não houver configuração por cliente:

| Opção | Default global |
| --- | --- |
| `Enabled` | `true` |
| `TotalTimeout` | `00:00:30` |
| `AttemptTimeout` | `00:00:10` |
| `RetryCount` | `3` |
| `RetryDelay` | `00:00:02` |
| `RetryUnsafeHttpMethods` | `false` |
| `CircuitBreakerFailureRatio` | `0.1` |
| `CircuitBreakerMinimumThroughput` | `100` |
| `CircuitBreakerSamplingDuration` | `00:00:30` |
| `CircuitBreakerBreakDuration` | `00:00:05` |

Overrides efetivos versionados em `src/TransferService.Worker/appsettings.json`:

| Cliente | Timeout total | Timeout por tentativa | Retries | Delay/backoff | Failure ratio | Minimum throughput | Sampling duration | Break duration |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Ledger` | `00:00:30` | `00:00:10` | `3` | `00:00:02`, constante | `0.1` | `4` | `00:00:30` | `00:00:05` |
| `Keycloak` | `00:00:15` | `00:00:05` | `2` | `00:00:01`, constante | `0.1` | `20` | `00:00:30` | `00:00:05` |

O arquivo do worker também contém uma seção `HttpResilience:Clients:JWKS`, mas o `TransferService.Worker` não registra o client `JWKS`; portanto, essa seção não é comportamento efetivo do worker no código atual.

Defaults derivados de `Jwt:*` nas APIs:

| Origem | Valor atual |
| --- | --- |
| `Jwt:JwksTimeoutSeconds` | `5` em `LedgerService.Api` e `BalanceService.Api` |
| `Jwt:JwksRetryCount` | `2` em `LedgerService.Api` e `BalanceService.Api` |
| `Jwt:JwksRetryBaseDelayMilliseconds` | `200` em `LedgerService.Api` e `BalanceService.Api` |
| `HttpResilience:Clients:JWKS:AttemptTimeout` derivado | `00:00:05` |
| `HttpResilience:Clients:JWKS:RetryCount` derivado | `2` |
| `HttpResilience:Clients:JWKS:RetryDelay` derivado | `00:00:00.200` |
| `HttpResilience:Clients:JWKS:TotalTimeout` derivado | `00:00:15.400` |
| Circuit breaker do `JWKS` nas APIs | defaults globais, salvo override explícito em `HttpResilience:Clients:JWKS:*` |

### Erros tratados como transitórios

A política usa o predicado padrão do `Microsoft.Extensions.Http.Resilience` para `HttpRetryStrategyOptions`. Os testes do repositório cobrem explicitamente:

- `HttpRequestException`;
- timeout da tentativa, observado como `TimeoutRejectedException`;
- respostas HTTP transitórias como `503 ServiceUnavailable`.

A documentação operacional do TransferService também registra `408 RequestTimeout`, `429 TooManyRequests` e respostas `5xx` como transitórias para os clients `Ledger` e `Keycloak`, alinhado ao handler padrão da biblioteca.

### Erros que não geram retry nem circuit breaker

Os testes do repositório cobrem explicitamente que os seguintes status não fazem retry e não contam para abrir o circuit breaker:

- `400 BadRequest`;
- `401 Unauthorized`;
- `403 Forbidden`;
- `404 NotFound`.

Esses status representam erro de contrato, autenticação/autorização, credencial inválida, escopo/audience incorretos ou recurso inexistente. No fluxo do `TransferService.Worker`, eles devem seguir para o tratamento funcional da Saga ou para a exceção específica do client, sem amplificação por retry HTTP.

### Como configurar

As opções por cliente ficam em `HttpResilience:Clients:<ClientName>`.

Para JWKS, as APIs ainda aceitam as chaves históricas:

- `Jwt:JwksTimeoutSeconds`;
- `Jwt:JwksRetryCount`;
- `Jwt:JwksRetryBaseDelayMilliseconds`.

Essas chaves alimentam defaults do client `JWKS`. Qualquer valor em `HttpResilience:Clients:JWKS:*` sobrescreve a política compartilhada sem alterar o contrato `Jwt:*`.

### Como testar localmente

Testes automatizados focados na política:

```powershell
dotnet test ./tests/TransferService.Worker.Tests/TransferService.Worker.Tests.csproj --configuration Release --filter "FullyQualifiedName~HttpClient"
```

Smoke funcional mínimo do fluxo TransferService -> Keycloak -> Ledger:

```powershell
./scripts/local/start-stack.ps1
./scripts/performance/run-loadtests.ps1 -Scenario transfer-fullstack-kafka
```

Para testar manualmente abertura de circuito, reduza temporariamente `CircuitBreakerMinimumThroughput`, `CircuitBreakerSamplingDuration` e `CircuitBreakerBreakDuration` por variável de ambiente do client desejado, deixe a dependência indisponível e acompanhe os logs do processo que faz a chamada. Não versione esses overrides.

### Como observar

Os logs da política HTTP resiliente usam:

- `Warning` para retry agendado;
- `Warning` para circuito aberto;
- `Warning` para chamada rejeitada por circuito aberto;
- `Information` para half-open;
- `Information` para fechamento do circuito.

Em 2026-06-25, a política compartilhada em `HttpResilienceDefaults` passou a emitir logs e métricas de resiliência HTTP com `System.Diagnostics.Metrics`.

Sinais emitidos:

- retries por cliente;
- timeouts por cliente;
- circuit breaker open, half-open e closed;
- chamadas rejeitadas por circuito aberto;
- duração das chamadas HTTP resilientes.

Tags de baixa cardinalidade:

- `client`;
- `dependency`;
- `operation`, quando disponível;
- `outcome`;
- `exception_type`, quando aplicável.

Clientes instrumentados:

- `JWKS`, usado pelas APIs para validação JWT;
- `Keycloak`, usado pelo token provider client credentials do `TransferService.Worker`;
- `Ledger`, usado pelo `TransferService.Worker` para chamar o `LedgerService.Api`.

Os logs preservam o escopo de correlação existente quando disponível e não registram segredo, token, client secret, URL completa nem payload sensível.

Com OpenTelemetry habilitado no processo host, o meter `HttpResilienceDefaults` é exportado junto com as demais métricas. A referência operacional dos nomes das métricas fica em `docs/observability.md`.

## Lacunas restantes

- A parte HTTP desta ADR está implementada para `JWKS`, `Keycloak` e `Ledger`.
- A parte de startup/readiness permanece tratada por ADRs e documentação próprias. No estado atual, readiness das APIs valida o PostgreSQL necessário para aceitar tráfego HTTP; Kafka e publicação/consumo assíncronos pertencem aos workers e não bloqueiam readiness das APIs.
