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

## Avaliação de ADR

Antes de alterar código, avaliar se o ajuste exige uma ADR.

Uma ADR deve ser criada quando o ajuste envolver pelo menos um dos pontos abaixo:

- mudança de arquitetura ou organização entre camadas;
- introdução, remoção ou alteração de padrão arquitetural;
- mudança em contrato HTTP, eventos, mensagens, DTOs públicos ou integração entre serviços;
- decisão sobre persistência, transação, idempotência, outbox, DLQ, retry, re-drive ou consistência;
- decisão de segurança, autenticação, autorização, headers, OWASP ou exposição de endpoint;
- mudança em observabilidade, logs, tracing, correlation id, métricas ou auditoria;
- alteração relevante de dependência, biblioteca, framework ou infraestrutura;
- alteração que afete manutenção futura, testabilidade, escalabilidade, resiliência ou operação.

Não criar ADR para:

- correção pequena e localizada;
- ajuste de nome sem impacto arquitetural;
- formatação;
- pequenas refatorações internas sem mudança de decisão técnica;
- ajuste de teste sem decisão arquitetural nova;
- documentação simples de comportamento já existente.

Quando uma ADR for necessária:

1. Identificar o próximo número sequencial em `docs/adrs`.
2. Criar o arquivo no formato `ADR-XXXX-titulo-curto-em-kebab-case.md`, se esse for o padrão do repositório.
3. Seguir o modelo abaixo.
4. Referenciar os arquivos/projetos afetados na seção "Decisão".
5. Registrar benefícios, trade-offs e alternativas consideradas.
6. Manter o texto objetivo, técnico e compatível com o escopo real do ajuste.
7. Não inventar decisões que não foram implementadas.

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

## Finalização do ajuste

Ao concluir uma alteração no serviço .NET:

1. Revisar o diff.
2. Executar restore, build e testes relevantes.
3. Criar ou atualizar ADR em `docs/adrs` quando a mudança envolver decisão arquitetural.
4. Se o usuário pediu commit, criar commit semântico usando Conventional Commits.
5. A mensagem do commit deve refletir o tipo predominante da alteração.

Exemplos:

- `feat: adicionar fluxo de re-drive para outbox failed`
- `fix: corrigir propagação de correlation id`
- `refactor: isolar lógica de dispatcher`
- `test: adicionar cobertura para retry policy`
- `docs: registrar ADR sobre uso de outbox`

## restore e build
```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
