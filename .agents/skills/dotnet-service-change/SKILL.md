---
name: dotnet-service-change
description: Use esta skill ao alterar serviços .NET deste repositório, especialmente código de API, Application, Domain, Infrastructure, EF Core, Kafka, Outbox, autenticação, configuração e testes.
---

# Objetivo

Executar mudanças em serviços .NET deste repositório com segurança e consistência, respeitando:

- Clean Architecture
- DDD
- Central Package Management
- regras de build, testes e estilo já definidas no repositório
- documentação existente em README e ADRs

# Quando usar

Use esta skill quando a tarefa envolver qualquer um dos itens abaixo:

- endpoints HTTP
- controllers
- middlewares
- handlers
- services de aplicação
- entidades ou regras de domínio
- EF Core
- migrations
- Kafka
- Outbox
- autenticação/autorização
- configurações de execução
- testes unitários ou de integração

# Antes de alterar

1. Identifique a camada afetada:
   - Api
   - Application
   - Domain
   - Infrastructure

2. Verifique se a mudança afeta:
   - contrato HTTP
   - DI
   - autenticação/autorização
   - EF Core / migrations
   - Kafka / Outbox
   - testes
   - documentação

3. Consulte os arquivos relevantes:
   - `AGENTS.md`
   - `README.md`
   - `docs/adrs/`
   - `Directory.Packages.props`
   - `Directory.Build.props`
   - `.editorconfig`
   - `global.json`
   - `coverlet.runsettings`

# Regras de implementação

- Faça a menor mudança possível.
- Preserve as fronteiras entre camadas.
- Não adicione `Version=` em `PackageReference`.
- Não mova regra de negócio para controller, endpoint ou middleware.
- Não coloque detalhes de infraestrutura na camada `Domain`.
- Não altere migrations existentes sem necessidade explícita.
- Não crie drift entre código, README, workflow, arquivos de VS Code e configuração local.
- Atualize documentação quando a mudança alterar comportamento, contrato, fluxo arquitetural ou execução local.

# Fluxo recomendado

## 1. Entender o impacto
- localizar o ponto de entrada da mudança
- localizar os testes relacionados
- identificar dependências tocadas
- identificar se a mudança é local ou transversal

## 2. Implementar
- alterar apenas os arquivos necessários
- preservar estilo e padrões existentes
- manter coerência com o restante da solução

## 3. Validar
- rodar build
- rodar testes proporcionais ao impacto
- revisar documentação afetada

# Validações padrão

## restore e build
```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
