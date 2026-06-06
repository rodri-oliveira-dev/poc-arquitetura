---
name: ddd-implementation-vernon
description: Use esta skill quando a tarefa envolver implementação, revisão ou refatoração orientada a Domain-Driven Design em código .NET/C#, especialmente bounded contexts, linguagem ubíqua, aggregates, entidades, value objects, repositories, domain events, application services, anti-corruption layer, integração entre contextos, separação entre Domain, Application e Infrastructure, ou revisão de PR com impacto no modelo de domínio. Não use esta skill para CRUD simples, ajustes puramente técnicos ou aplicação cerimonial de DDD sem invariantes, ciclo de vida ou linguagem de negócio relevantes.
---

# DDD Implementation Vernon

Atue como um revisor e implementador sênior de Domain-Driven Design aplicado a sistemas .NET. Use esta skill como orientação complementar ao `AGENTS.md` do repositório. Quando houver conflito, preserve as regras locais do projeto.

Esta skill é uma adaptação em português para Codex baseada em materiais MIT inspirados em `Implementing Domain-Driven Design`, de Vaughn Vernon, com ajustes para este repositório .NET, Clean Architecture, Pub/Sub, Outbox, PostgreSQL e testes automatizados.

## Quando usar

Use esta skill quando a mudança envolver:

- `src/LedgerService.Domain`
- `src/LedgerService.Application`
- `src/LedgerService.Infrastructure`
- `src/BalanceService.Domain`
- `src/BalanceService.Application`
- `src/BalanceService.Infrastructure`
- `tests/*`, quando os testes validarem comportamento de domínio, aplicação, eventos, persistência ou integração
- `docs/adrs/` e `docs/architecture/`, quando a decisão afetar fronteiras, contexto, modelo, eventos, mensageria, persistência ou integração

Use também quando a tarefa mencionar aggregate, entidade, value object, repository, domain event, application service, invariant, bounded context, ubiquitous language, anticorruption layer, context map, event sourcing, consistência eventual ou consistência transacional.

## Viés principal a corrigir

DDD prático não é CRUD renomeado. Antes de criar classes, services, repositories ou eventos, identifique o contexto, a linguagem local, as invariantes e o limite de consistência. Não transforme o domínio em um conjunto de DTOs persistidos com nomes mais bonitos.

Ao mesmo tempo, não aplique DDD como cerimônia. Se o fluxo for CRUD simples, sem invariantes importantes, ciclo de vida relevante ou linguagem ambígua, mantenha a solução simples e explícita.

## Regras de decisão

### Contexto e linguagem

- Nomeie o Bounded Context antes de interpretar termos, módulos, serviços, repositories, eventos, APIs, persistência ou integrações.
- Não force um modelo global único para conceitos que podem ter significados diferentes em `LedgerService`, `BalanceService`, autenticação, mensageria ou infraestrutura.
- Use a linguagem ubíqua local de forma consistente em código, testes, comandos, eventos, application services, repositories e documentação.
- Um conceito deve ter um termo claro dentro do contexto. Um mesmo termo não deve carregar significados diferentes no mesmo contexto.
- Quando um termo estiver ambíguo, genérico ou técnico demais, renomeie ou separe o conceito antes de continuar codificando.

### Core Domain e complexidade

- Invista mais modelagem onde há regra de negócio, invariantes, ciclo de vida, risco operacional ou diferenciação arquitetural.
- Mantenha subdomínios genéricos ou de suporte mais simples.
- Não crie aggregates, domain services, factories ou repositories ricos apenas para parecer arquitetural.
- Proteja o domínio de termos de fornecedor, framework, banco, fila, contrato HTTP ou payload externo.

### Aggregates

- Trate Aggregate como limite de consistência imediata, não como espelho de tabela.
- Mantenha aggregates pequenos e orientados por invariantes.
- Exponha uma única raiz para alteração de estado.
- Encapsule coleções e estado mutável.
- Prefira métodos que revelem intenção de negócio em vez de setters genéricos.
- Referencie outros aggregates por identidade, não por grafo de objetos carregado.
- Prefira uma raiz de aggregate por transação. Quando múltiplas raízes forem alteradas, documente a invariante que exige isso ou use coordenação por eventos/processos.

### Entidades e Value Objects

- Use entidade quando identidade e ciclo de vida importarem.
- Métodos de entidade devem proteger transições de estado relevantes, não apenas alterar propriedades.
- Use Value Objects imutáveis para conceitos descritivos com significado de domínio, como identificadores, valores monetários, faixas, status de negócio, nomes e quantidades.
- Valide Value Objects na criação e compare por valor.
- Evite primitives, strings, números, flags e enums quando eles escondem regra de negócio importante.

### Domain Services

- Use Domain Service apenas para operação significativa de domínio que envolve múltiplos objetos e não pertence naturalmente a uma entidade ou Value Object.
- Não coloque serialização, mapeamento de transporte, persistência, consulta SQL, integração externa ou regra de framework em Domain Service.
- Se um Domain Service começar a acumular decisões de fluxo de aplicação, reavalie se isso pertence à camada `Application`.

### Repositories e persistência

- Repository deve trabalhar com Aggregate Root ou necessidade explícita de aplicação, não com tabelas genéricas.
- Interfaces de repository devem expressar intenção de domínio ou caso de uso.
- Não exponha `IQueryable` se isso vazar detalhes de persistência ou permitir regras fora do modelo.
- Não coloque regra de negócio dentro da implementação do repository.
- EF Core, mappings, migrations, transações, índices e detalhes de PostgreSQL pertencem à infraestrutura.
- O domínio não deve depender de `DbContext`, atributos de EF, tipos de provider, schema do banco ou shape de tabela.

### Domain Events e Integration Events

- Publique Domain Events apenas para fatos de negócio concluídos e relevantes.
- Nomeie eventos como fatos no passado, por exemplo `LedgerEntryPosted`, `TransferReversed`, `BalanceProjectionUpdated`, quando fizer sentido no domínio.
- Não crie evento para toda alteração de propriedade.
- Não use evento para esconder aggregate mal desenhado.
- Mantenha payload de Domain Event coerente com o modelo local.
- Diferencie Domain Event interno de Integration Event usado fora do contexto.
- Contratos de mensageria, headers, correlation id, idempotência, ordering key, DLQ e Outbox devem ser tratados nas bordas de aplicação/infraestrutura conforme as regras do repositório.

### Application Services

- Application Service coordena caso de uso. Ele carrega aggregates, chama comportamento de domínio, persiste resultado, publica ou agenda eventos e coordena transação/integração.
- Application Service não deve virar o verdadeiro domínio com `if`/`switch` cheios de regra de negócio.
- Controllers, endpoints e workers devem ficar finos e delegar a orquestração correta.
- Validação de entrada pode ocorrer na borda da aplicação, mas invariantes de domínio precisam permanecer protegidas no modelo.

### Integração entre contextos

- Toda interação entre contextos deve deixar explícito relacionamento, responsabilidade de tradução e direção de influência.
- Não importe diretamente modelo de domínio de outro contexto para evitar acoplamento semântico.
- Use DTOs, adapters, translators, anti-corruption layer, projections ou contracts quando dados atravessarem fronteiras.
- Não deixe payload externo, status de fornecedor, schema legado, contrato HTTP ou mensagem de fila definir o modelo local.

### Organização de código

- Preserve a separação `Api`, `Application`, `Domain` e `Infrastructure`.
- Organize código por contexto e responsabilidade de domínio antes de criar pastas genéricas.
- Evite `Shared`, `Common`, `Helpers`, `Utils` ou abstrações amplas para conceitos de domínio.
- Compartilhamento só deve ocorrer quando houver conceito estável, governado e realmente comum.

## Gatilhos de revisão

Interrompa a implementação e revise o desenho quando:

- Uma classe de aplicação, endpoint ou handler concentrar decisões de negócio.
- Um aggregate depender diretamente de EF Core, DTO, HTTP, Pub/Sub, Kafka, JSON ou configuração.
- Um repository parecer DAO genérico, retornar linha/tabela ou aplicar regra de negócio.
- Um evento parecer comando, ser vago ou representar apenas mudança de campo.
- Um fluxo tentar alterar vários aggregates sem explicar a invariante transacional.
- Um termo de domínio aparecer com nomes diferentes em código, testes e documentação.
- Um conceito externo entrar sem tradução no domínio local.
- A solução criar cerimônia de DDD para CRUD simples.

## Processo recomendado para Codex

Ao alterar código com impacto em DDD:

1. Leia `AGENTS.md`, `README.md` e ADRs relacionadas.
2. Identifique o bounded context afetado.
3. Liste os termos de linguagem ubíqua que aparecem na mudança.
4. Localize invariantes e transições de estado envolvidas.
5. Decida se a regra pertence a Domain, Application ou Infrastructure.
6. Faça a menor mudança coerente com o modelo.
7. Atualize testes de comportamento de domínio ou aplicação.
8. Atualize documentação ou ADR se a fronteira, contrato, evento, persistência ou integração mudar.
9. Explique no resumo final o que mudou, o motivo e como validar.

## Checklist final

Antes de concluir:

- O bounded context está explícito?
- Os nomes refletem a linguagem local?
- A regra de negócio ficou no domínio quando deveria?
- A camada Application apenas coordena o caso de uso?
- O aggregate é pequeno, protegido pela raiz e orientado por invariantes?
- Outros aggregates são referenciados por identidade?
- Value Objects protegem conceitos importantes?
- Repositories acessam Aggregate Roots ou necessidades claras de aplicação?
- Domain Events são fatos de negócio relevantes?
- Representações de HTTP, banco, fila e infraestrutura ficaram fora do domínio?
- Testes cobrem transições válidas, inválidas e efeitos relevantes?
- A mudança evitou cerimônia desnecessária?

## Fontes e atribuição

Adaptação baseada principalmente em:

- `ciembor/agent-rules-books`, arquivo `implementing-domain-driven-design/implementing-domain-driven-design.mini.md`.
- Livro de referência declarado pela fonte: `Implementing Domain-Driven Design`, Vaughn Vernon.

O material de origem está licenciado sob MIT. Ver `.agents/skills/THIRD_PARTY_NOTICES.md` para atribuição e aviso de licença.