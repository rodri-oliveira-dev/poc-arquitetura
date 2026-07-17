# Contribuindo

Obrigado por contribuir com esta POC. Mantenha mudancas pequenas, reproduziveis e alinhadas aos bounded contexts.

## Ambiente

- Instale o SDK .NET definido em `global.json`.
- Restaure ferramentas com `dotnet tool restore`.
- Use os scripts em `scripts/` quando existirem em vez de comandos improvisados.
- Nao versione secrets; use exemplos com placeholders e arquivos locais ignorados pelo Git.

## Branches e commits

- Nao trabalhe diretamente em `main`.
- Use branches curtas por objetivo, por exemplo `fix/time-provider-ledger`.
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:` ou `ci:`.

## Solutions

- Use a solution contextual quando a mudanca estiver restrita a um contexto: `LedgerService.slnx`, `BalanceService.slnx`, `TransferService.slnx`, `IdentityService.slnx`, `AuditService.slnx` ou `PaymentService.slnx`.
- Use `PocArquitetura.Shared.slnx` para Shared.
- Use `PocArquitetura.slnx` para mudancas transversais, arquitetura, governanca e validacao consolidada.

## Testes

Fluxo base:

```powershell
dotnet tool restore
dotnet restore .\PocArquitetura.slnx
dotnet build .\PocArquitetura.slnx --configuration Release --no-restore
dotnet test .\PocArquitetura.slnx --configuration Release --no-build --settings .\coverlet.runsettings
```

Para mudancas localizadas, rode primeiro o teste mais proximo. Para mudancas em contratos HTTP, gere OpenAPI e valide drift conforme `docs/development/openapi-contract-validation.md`.

## ADRs

Crie ou atualize ADR quando houver decisao arquitetural relevante: mensageria, persistencia, seguranca, observabilidade, estrutura de projeto, contratos entre servicos ou mudanca de comportamento publico. Nao crie ADR para pequenos ajustes internos sem decisao duradoura.

Use status canonicos: `Proposto`, `Aceito`, `Rejeitado`, `Substituido`, `Parcialmente substituido`, `Parcialmente implementado`.

Valide ADRs com:

```powershell
.\scripts\quality\validate-adrs.ps1
```

## Seguranca

- Nao publique vulnerabilidades exploraveis em issues.
- Nao adicione secrets, tokens ou credenciais reais.
- Revise autorizacao por merchant, scopes, issuer, audience e headers ao alterar endpoints protegidos.
- Preserve idempotencia, correlacao e contratos ao alterar eventos, Outbox, Inbox ou workers.

## Pull requests

Um PR deve explicar objetivo, escopo, validacoes executadas e riscos residuais. Inclua evidencias de build/test quando aplicavel e destaque qualquer mudanca de contrato, ADR, migration, workflow ou configuracao local.
