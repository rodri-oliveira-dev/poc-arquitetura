---
name: dotnet-service-change
description: Use esta skill ao alterar código de serviços .NET deste repositório, especialmente APIs, Application, Domain, Infrastructure, EF Core, Kafka, Outbox, autenticação ou testes.
---

# Objetivo
Aplicar mudanças em serviços .NET deste repositório respeitando:
- Clean Architecture
- DDD
- Central Package Management
- regras já definidas em README, workflow e editorconfig

# Antes de alterar
1. Identifique a camada afetada:
   - Api
   - Application
   - Domain
   - Infrastructure
2. Verifique se a mudança afeta:
   - contrato HTTP
   - DI
   - EF Core / migrations
   - Kafka / Outbox
   - autenticação/autorização
   - testes
3. Consulte:
   - `AGENTS.md`
   - `README.md`
   - `Directory.Packages.props`
   - `Directory.Build.props`
   - `.editorconfig`

# Regras de implementação
- Faça a menor mudança possível.
- Preserve as fronteiras entre camadas.
- Não adicione `Version=` em `PackageReference`.
- Não mova regra de negócio para controller/endpoint.
- Não coloque detalhe de infraestrutura na camada Domain.
- Não altere migrations existentes sem necessidade explícita.

# Validações padrão
## fluxo mínimo
- `dotnet restore LedgerService.slnx`
- `dotnet build LedgerService.slnx --configuration Release --no-restore`

## testes
- localizados primeiro, quando possível
- solução inteira se houver impacto transversal

## comando padrão de testes
- `dotnet test LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`

# Casos especiais
## mudança em API
- revisar contrato
- revisar autenticação/autorização
- revisar documentação relevante
- revisar testes de integração

## mudança em EF Core
- verificar necessidade de migration
- validar DbContext, mapeamentos e testes

## mudança em Kafka/Outbox
- preservar correlação, headers e idempotência
- revisar consumidores/publicadores e testes associados

# Entrega esperada
Ao concluir, informar:
- arquivos alterados
- resumo do impacto
- testes executados
- riscos e pendências
