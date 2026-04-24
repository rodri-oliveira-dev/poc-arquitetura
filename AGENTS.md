# AGENTS.md

## Objetivo

Este repositório é uma POC de microserviços em .NET com:

- Clean Architecture
- DDD
- PostgreSQL
- Kafka
- Outbox
- autenticação JWT com JWKS
- testes automatizados
- documentação por README e ADRs

O objetivo do agente é fazer mudanças pequenas, corretas, reprodutíveis e coerentes com a arquitetura já adotada.

## Fontes principais de verdade

Antes de alterar qualquer coisa, consulte nesta ordem quando relevante:

1. `README.md`
2. `docs/adrs/`
3. `Directory.Packages.props`
4. `Directory.Build.props`
5. `.editorconfig`
6. `global.json`
7. `coverlet.runsettings`
8. `LedgerService.slnx`

## Escopo do repositório

A solução principal do repositório é:

- `LedgerService.slnx`

Os principais componentes estão organizados em:

- `src/Auth.Api`
- `src/LedgerService.Api`
- `src/LedgerService.Application`
- `src/LedgerService.Domain`
- `src/LedgerService.Infrastructure`
- `src/BalanceService.Api`
- `src/BalanceService.Application`
- `src/BalanceService.Domain`
- `src/BalanceService.Infrastructure`
- `tests/*`

## Regras obrigatórias

- Faça a menor mudança possível para resolver o problema.
- Preserve as fronteiras entre `Api`, `Application`, `Domain` e `Infrastructure`.
- Não mova regra de negócio para controller, endpoint, middleware ou camada de infraestrutura.
- Não coloque detalhes de infraestrutura na camada `Domain`.
- Não adicione `Version=` em `PackageReference`. O repositório usa Central Package Management.
- Não altere migrations existentes sem necessidade explícita.
- Não introduza segredos no repositório.
- Não use URLs, portas ou comandos inventados. Prefira o que já estiver documentado no repo.
- Quando houver mudança de contrato, fluxo arquitetural, setup local ou comportamento relevante, atualize a documentação correspondente.

## Convenções de implementação

### Dependências
- Use versões centralizadas em `Directory.Packages.props`.
- Prefira reutilizar dependências já existentes no repositório.
- Evite adicionar novos pacotes sem necessidade clara.

### Estilo e qualidade
- Respeite `.editorconfig`.
- Respeite `Nullable` e `ImplicitUsings` habilitados no repositório.
- Mantenha nomenclatura consistente com o padrão existente.
- Evite refactors amplos não solicitados.
- Evite renomeações desnecessárias.
- Evite alterar formatação de arquivos sem necessidade funcional.

### Arquitetura
- `Api` deve orquestrar entrada e saída HTTP.
- `Application` deve conter casos de uso, handlers, services e orquestração da aplicação.
- `Domain` deve conter regras e modelos de domínio sem dependência de infraestrutura.
- `Infrastructure` deve conter EF Core, integrações externas, Kafka, persistência e detalhes técnicos.

### EF Core
- Verifique se a mudança exige migration.
- Preserve compatibilidade entre entidades, mapeamentos e `DbContext`.
- Não modifique migrations antigas apenas para “organizar”.
- Se criar migration, ela deve refletir uma mudança real de schema.

### Kafka e Outbox
- Preserve correlação, headers, idempotência e contrato de eventos.
- Não quebre fluxo de publicação e consumo existente sem ajustar os testes e a documentação.
- Mudanças em eventos devem ser tratadas com cautela, pois podem afetar produtores, consumidores e projeções.

### Autenticação e autorização
- Preserve o comportamento de JWT Bearer e JWKS.
- Revise `issuer`, `audience`, scopes e policies ao alterar endpoints protegidos.
- Não relaxe segurança sem instrução explícita.

## Fluxo padrão antes de editar

1. Identifique a área afetada.
2. Identifique a camada afetada.
3. Verifique se há impacto em:
   - contrato HTTP
   - DI
   - autenticação/autorização
   - EF Core / migrations
   - Kafka / Outbox
   - testes
   - documentação
4. Localize os testes existentes relacionados à mudança.
5. Faça a menor alteração possível.

## Commits

- Quando o usuário solicitar que os ajustes sejam commitados, criar commits usando Conventional Commits.
- Usar o formato:
  - feat: para novas funcionalidades
  - fix: para correções
  - refactor: para refatorações sem alteração funcional
  - test: para criação ou ajuste de testes
  - docs: para documentação
  - chore: para ajustes operacionais, tooling ou configuração
- A mensagem deve ser objetiva, em português ou inglês conforme o padrão já usado no histórico do repositório.
- Antes de commitar, revisar o diff e executar os testes/checks relevantes.
- Não criar commit se houver falha de build/teste sem registrar claramente o motivo.

## ADRs

- Antes de implementar qualquer ajuste, avaliar se a mudança exige uma ADR.
- Criar ADR quando a mudança envolver decisão arquitetural, padrão técnico, contrato entre serviços, estratégia de persistência, mensageria, observabilidade, segurança, resiliência, integração externa, estrutura de projeto ou alteração relevante de comportamento.
- Não criar ADR para ajustes puramente mecânicos, correções triviais, renomeações locais, pequenas limpezas ou mudanças sem impacto arquitetural.
- Quando uma ADR for necessária, criar o arquivo em `docs/adrs`.
- Seguir o modelo de ADR existente no repositório, mantendo as seções: Status, Data, Contexto, Decisão, Consequências, Benefícios, Trade-offs / custos e Alternativas consideradas.
- Numerar a ADR usando o próximo número sequencial disponível.

## Comandos padrão

Use estes comandos como baseline:

```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
