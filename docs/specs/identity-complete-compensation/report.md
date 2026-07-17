# Relatorio

## Diagnostico

Confirmado. A implementacao anterior criava o usuario no Keycloak e marcava o
efeito externo como concluido antes de entrar no bloco compensavel. As operacoes
de geracao do `MerchantId`, criacao de value objects e registro do agregado
ficavam fora do `try/catch` que remove o usuario externo.

## Sequencia anterior

```text
Criar usuario no Keycloak
Marcar usuario externo como criado
Gerar MerchantId
Construir MerchantId, Email e Username
Criar User e UserRegisteredDomainEvent
Entrar no try/catch
Persistir localmente
```

## Sequencia final

```text
Criar usuario no Keycloak
Marcar usuario externo como criado
Entrar na regiao compensavel
Gerar MerchantId
Construir MerchantId, Email e Username
Criar User e UserRegisteredDomainEvent
Persistir localmente
Confirmar commit local
```

## Decisoes adotadas

- A regiao compensavel comeca imediatamente apos o `KeycloakUserId` ser
  conhecido.
- O ponto sem retorno continua sendo `LocalPersistenceConfirmed`.
- A compensacao existente foi reutilizada para evitar duplicidade de logica.
- A idempotencia manteve as mesmas categorias recuperaveis e bloqueantes.
- Nao foram alterados contratos HTTP, autorizacao, schema, migrations, Keycloak
  como emissor de tokens, Saga, Outbox ou Worker.

## Arquivos alterados

- `src/identity/IdentityService.Application/Users/Commands/CreateUserCommandHandler.cs`
- `tests/identity/IdentityService.UnitTests/Application/Users/Commands/CreateUserCommandHandlerTests.cs`
- `docs/specs/identity-complete-compensation/requirements.md`
- `docs/specs/identity-complete-compensation/design.md`
- `docs/specs/identity-complete-compensation/tasks.md`
- `docs/specs/identity-complete-compensation/report.md`
- `docs/development/identity-api.md`
- `docs/architecture/boundaries.md`
- `docs/adrs/0090-cadastro-usuarios-identity-service.md`
- `docs/adrs/0096-idempotencia-cadastro-usuarios-identity-service.md`
- `docs/adrs/0111-consistencia-cancelamento-cadastro-identity-service.md`
- `docs/README.md`

## Testes adicionados ou reforcados

- Falha no `IMerchantIdGenerator` apos criacao no Keycloak.
- Falha ao construir `MerchantId` apos criacao no Keycloak.
- Falha ao construir `Email` apos criacao no Keycloak.
- Falha ao construir `Username` apos criacao no Keycloak.
- Cancelamento durante `AddAsync` com compensacao por token independente.
- Compensacao que excede timeout sem mascarar a excecao original.
- Retry idempotente apos compensacao confirmada.
- Bloqueio de retry idempotente apos compensacao falha.

## Validacao

Comandos executados:

```powershell
dotnet test ./tests/identity/IdentityService.UnitTests/IdentityService.UnitTests.csproj --configuration Release
dotnet format ./IdentityService.slnx --include src/identity/IdentityService.Application/Users/Commands/CreateUserCommandHandler.cs tests/identity/IdentityService.UnitTests/Application/Users/Commands/CreateUserCommandHandlerTests.cs --verify-no-changes --verbosity minimal
dotnet restore ./IdentityService.slnx
dotnet build ./IdentityService.slnx --configuration Release --no-restore
dotnet test ./IdentityService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
dotnet tool restore
```

Resultados:

- `IdentityService.UnitTests`: 131 testes aprovados.
- `IdentityService.IntegrationTests`: 27 testes aprovados.
- `IdentityService.slnx` em Release: build aprovado sem avisos.
- `dotnet format --verify-no-changes`: aprovado para os arquivos C# alterados.
- `dotnet tool restore`: aprovado.

## Riscos residuais

- Commit ambiguo do PostgreSQL ainda pode exigir investigacao operacional.
- Falha real de compensacao no Keycloak pode deixar usuario externo sem vinculo
  local; retry automatico permanece bloqueado.

## Fora do escopo mantido

- Contratos HTTP, OpenAPI, scopes e policies.
- Persistencia ou comparacao de senha.
- Saga generica, Outbox, Worker ou reconciliacao assincrona.
- Mudancas em outros bounded contexts.
