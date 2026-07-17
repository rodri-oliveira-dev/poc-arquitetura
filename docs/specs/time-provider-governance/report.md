# TimeProvider e governanca residual - relatorio

## Tempo

Usos encontrados:

- `IClock/SystemClock` em Ledger, Balance, Transfer e Payment.
- `TimeProvider` ja existente em Identity e providers de token.
- `DateTimeOffset.UtcNow` residual em Audit, Balance Worker e gateways Payment.
- `DateTime.UtcNow` residual em `IdentityService.Domain.Users.User` como fallback opcional historico.
- Parametros opcionais com fallback para `new SystemClock()` em handlers, repositories e worker do Ledger.

Usos migrados:

- Handlers de Ledger para criacao, estorno, reprocessamento e requeue.
- Repositories de Ledger para Outbox e estorno.
- Worker de Outbox do Ledger, incluindo delay controlado por `TimeProvider`.
- Application/queries/replay do Balance e controller de consolidados.
- Application, Infrastructure e Worker do Transfer, incluindo saga, idempotencia, outbox e delays.
- Application, Infrastructure, API webhook e Worker do Payment, incluindo Inbox, idempotencia, retry e validação de assinatura.

Usos mantidos e justificativa:

- `TimeProvider.System.GetTimestamp()` em gateways Payment/FakePayment para duracao monotônica, sem regra de negocio.
- `DateTimeOffset.UtcNow` em adaptadores externos ou fallback de payload ainda nao migrados neste corte para evitar refactor amplo.
- `DateTime.UtcNow` em testes de dominio que passam instantes explicitos ao aggregate, sem consulta interna pelo aggregate.

Testes temporais cobertos:

- timestamp controlado no cadastro: `CreateLancamentoCommandHandlerTests`, `CreatePaymentCommandHandlerTests`, `SolicitarTransferenciaCommandHandlerTests`;
- expiracao de idempotencia: services de idempotencia Identity, Transfer e Payment com provider fixo;
- leases e locks: workers/repositories de Outbox, Inbox e Saga usam `TimeProvider`;
- retry e backoff: Outbox Ledger, Outbox Transfer e Inbox Payment usam provider controlavel;
- Outbox e Inbox: testes de Ledger Outbox, Transfer Outbox e Payment Inbox;
- estados expirados: claims com `LockedUntil`, `NextRetryAt` e assinatura Stripe;
- ausencia de dependencia do relogio real: fakes de teste derivam de `TimeProvider`;
- comportamento UTC: asserts existentes preservados em Ledger/Balance/Payment;
- horario local: regras usam `GetUtcNow()` e `DateTimeOffset` UTC, sem `Now`;
- concorrencia deterministica: testes de claim/lock usam instantes passados explicitamente.

Riscos residuais:

- Alguns adaptadores ainda possuem fallback temporal direto fora do dominio.
- Nem todos os testes que usam `DateTime.UtcNow` em dados auxiliares foram convertidos para constantes, porque nao exercem consulta de relogio pela producao.

## Governanca

Arquivos criados:

- `SECURITY.md`
- `CONTRIBUTING.md`
- `.github/CODEOWNERS`
- `scripts/quality/validate-adrs.ps1`
- `docs/specs/time-provider-governance/*`

Regras documentadas:

- reporte privado de vulnerabilidades;
- limites de seguranca da POC;
- branches/commits/solutions/testes/PR;
- criterios para ADR e validacao de contratos;
- ownership simples por bounded context, Shared, infraestrutura, docs, workflows e seguranca.

ADRs normalizados:

- status canonicos definidos no indice e no validador.
- relacoes historicas de substituicao permanecem preservadas no indice sem reescrever ADRs antigas.

Validacao adicionada:

- `.\scripts\quality\validate-adrs.ps1`

Itens deixados fora do escopo:

- reescrever ADRs historicas;
- corrigir duplicidade historica de numeracao de ADR;
- adicionar workflow obrigatorio para ADRs;
- transformar a POC em processo de seguranca produtivo.

## Validacao

Executado com sucesso:

- `.\scripts\quality\validate-adrs.ps1`
- `dotnet build .\PocArquitetura.slnx --configuration Release`
- `dotnet test .\LedgerService.slnx --configuration Release --no-build --settings .\coverlet.runsettings --filter "FullyQualifiedName!~IntegrationTests"`
- `dotnet test .\BalanceService.slnx --configuration Release --no-build --settings .\coverlet.runsettings --filter "FullyQualifiedName!~IntegrationTests"`
- `dotnet test .\TransferService.slnx --configuration Release --no-build --settings .\coverlet.runsettings --filter "FullyQualifiedName!~IntegrationTests"`
- `dotnet test .\tests\payment\PaymentService.UnitTests\PaymentService.UnitTests.csproj --configuration Release --no-build --settings .\coverlet.runsettings`
- `dotnet test .\tests\ledger\LedgerService.IntegrationTests\LedgerService.IntegrationTests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\balance\BalanceService.IntegrationTests\BalanceService.IntegrationTests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\transfer\TransferService.IntegrationTests\TransferService.IntegrationTests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\payment\PaymentService.IntegrationTests\PaymentService.IntegrationTests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\identity\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\audit\AuditService.Api.Tests\AuditService.Api.Tests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\audit\AuditService.Infrastructure.Tests\AuditService.Infrastructure.Tests.csproj --configuration Release --settings .\coverlet.runsettings`
- `dotnet test .\tests\Architecture.Tests\Architecture.Tests.csproj --configuration Release --settings .\coverlet.runsettings`

Executado com ressalva:

- `dotnet test .\PocArquitetura.slnx --configuration Release --no-build --settings .\coverlet.runsettings` ainda falhou na execucao agregada paralela por instabilidade da Docker-compatible API/Testcontainers (`Invalid chunk header`) em fixtures PostgreSQL, embora os projetos de integracao afetados tenham passado isoladamente.
