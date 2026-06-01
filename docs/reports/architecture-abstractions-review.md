# Revisao de abstracoes arquiteturais

## Objetivo

Revisar interfaces com implementacao unica e remover apenas abstracoes sem boundary, variacao ou ganho relevante de teste.

## Classificacao

| Abstracoes | Decisao | Motivo |
| --- | --- | --- |
| `IDailyBalanceService`, `IPeriodBalanceService`, `DailyBalanceService`, `PeriodBalanceService` | Remover | Camada intermediaria sem variacao real. Os handlers MediatR apenas encaminhavam chamadas e agora concentram os casos de uso de consulta. |
| Repositories e transacoes de Ledger e Balance | Manter, porque representa boundary real | Portas de persistencia isolam Application/Domain dos adapters EF Core e PostgreSQL. |
| `IClock` de Ledger e Balance | Manter, porque facilita teste de comportamento importante | Permite controlar tempo em regras, respostas e testes. |
| `IOutboxMessagePublisher`, `IDeadLetterPublisher` | Manter, porque representa boundary real | Isolam mensageria dos adapters Kafka e permitem simular falhas de publicacao. |
| `IRetryStrategy`, `IJitterProvider` | Manter, porque facilita teste de comportamento importante | Tornam retry e jitter deterministas nos testes do publisher Outbox. |
| `IRsaKeyProvider`, `IJwtIssuer` do `Auth.Api` legado | Manter, porque representa boundary real | Isolam chave RSA persistida e emissao de token no componente legado. |
| `IMerchantAuthorizationService` das APIs | Adiar, porque a remocao teria risco maior que o ganho | Sao politicas de seguranca na borda HTTP. A troca por classe concreta seria local, mas nao reduz encaminhamento entre camadas. |
| `IAggregateRoot` de Ledger e Balance | Manter | Marcador explicito de dominio; nao cria encaminhamento nem acoplamento de runtime. |

## Resultado

As consultas diaria e por periodo do `BalanceService` passaram a ser implementadas diretamente por `GetDailyBalanceHandler` e `GetPeriodBalanceHandler`. Nao houve alteracao de rotas, URLs, payloads, status codes, persistencia ou mensageria.

Nao foi criada ADR: a mudanca remove overhead local e preserva as decisoes arquiteturais existentes.
