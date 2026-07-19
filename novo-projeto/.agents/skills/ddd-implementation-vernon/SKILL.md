---
name: ddd-implementation-vernon
description: Use esta skill para implementar, revisar ou refatorar código .NET orientado a DDD, especialmente bounded contexts, aggregates, entidades, Value Objects, repositories, eventos, serviços de aplicação e integração entre módulos. Não use para CRUD simples ou ajustes puramente técnicos sem regra de negócio relevante.
---

# Objetivo

Aplicar Domain-Driven Design de forma pragmática no código .NET, protegendo linguagem, invariantes e limites de consistência sem transformar o projeto em DDD cerimonial.

## Princípio central

DDD não é CRUD renomeado. Antes de criar classes, interfaces, repositories ou eventos, identifique:

- o bounded context;
- a linguagem local;
- as invariantes;
- o ciclo de vida;
- o limite de consistência;
- o risco de concorrência.

Se o fluxo for CRUD simples, mantenha a solução simples.

## Contexto e linguagem

- Nomeie o bounded context antes de interpretar conceitos.
- Não force um modelo global para `Pet`, `Appointment`, `Attendance`, `Service`, `Professional` ou `Customer`.
- Use a linguagem ubíqua local em código, testes e documentação.
- Separe conceitos quando o mesmo termo tiver significados diferentes.
- Proteja o domínio de termos de framework, banco, fila, fornecedor ou contrato HTTP.

## Aggregates

- Aggregate é limite de consistência imediata, não espelho de tabela.
- Mantenha aggregates pequenos e orientados por invariantes.
- Exponha uma única raiz para alteração de estado.
- Encapsule coleções e estado mutável.
- Prefira métodos que expressem intenção, como `Schedule`, `Reschedule`, `Cancel`, `CheckIn` e `Complete`.
- Referencie outros aggregates por identidade.
- Prefira uma raiz por transação.
- Quando várias raízes precisarem mudar, avalie coordenação por caso de uso, evento ou processo.

## Entidades e Value Objects

- Use entidade quando identidade e ciclo de vida importarem.
- Use Value Objects imutáveis para conceitos como intervalo de horário, duração, porte, espécie, telefone, endereço e restrição de atendimento.
- Valide Value Objects na criação.
- Evite strings, números e flags quando esconderem regra relevante.
- Não crie Value Object apenas para embrulhar uma primitive sem semântica.

## Domain Services

Use Domain Service somente para uma operação de domínio relevante que envolva múltiplos objetos e não pertença naturalmente a uma entidade ou Value Object.

Não coloque em Domain Service:

- persistência;
- serialização;
- SQL;
- HTTP;
- mapeamento de DTO;
- decisões de fluxo da aplicação.

## Repositories e persistência

- Repositories devem trabalhar com Aggregate Roots ou necessidades explícitas do caso de uso.
- Interfaces devem expressar intenção, não operações genéricas de tabela.
- Não exponha `IQueryable` quando isso permitir regras fora do modelo.
- Não coloque regra de negócio na implementação do repository.
- EF Core, mappings, migrations, índices e transações pertencem à infraestrutura.

## Eventos

- Domain Events representam fatos de negócio concluídos e relevantes.
- Nomeie eventos no passado, como `AppointmentScheduled`, `AppointmentCancelled` ou `PetCheckedIn`.
- Não crie evento para toda alteração de propriedade.
- Diferencie Domain Event interno de Integration Event publicado fora do módulo.
- Não use eventos para esconder um aggregate mal desenhado.

## Application Services

- Coordenam casos de uso.
- Carregam aggregates, invocam comportamento, persistem e coordenam efeitos.
- Não devem concentrar as verdadeiras regras do domínio em grandes `if` ou `switch`.
- Controllers e endpoints devem permanecer finos.
- Validação de entrada pode ocorrer na aplicação, mas invariantes devem continuar protegidas no domínio.

## Integração entre módulos

- Explicite direção de influência e responsabilidade de tradução.
- Não importe diretamente o modelo de domínio de outro módulo.
- Use contratos, DTOs, adapters, translators, projections ou anti-corruption layer quando necessário.
- Não deixe payload externo definir o modelo local.

## Processo

1. Leia `AGENTS.md` e os documentos de domínio relevantes.
2. Identifique o bounded context.
3. Liste os termos envolvidos.
4. Localize invariantes e transições de estado.
5. Decida se cada regra pertence a Domain, Application ou Infrastructure.
6. Faça a menor mudança coerente.
7. Atualize testes de comportamento.
8. Atualize documentação ou ADR quando fronteira, contrato ou persistência mudar.
9. Explique o resultado e as validações.

## Checklist

- O bounded context está explícito?
- Os nomes refletem a linguagem local?
- O aggregate protege invariantes?
- A Application apenas coordena?
- Outros aggregates são referenciados por identidade?
- Value Objects protegem conceitos relevantes?
- Domain Events são fatos úteis?
- HTTP, banco e infraestrutura ficaram fora do Domain?
- Testes cobrem transições válidas e inválidas?
- A mudança evitou cerimônia desnecessária?

## Fontes e atribuição

Adaptação operacional em português inspirada em material MIT do projeto `ciembor/agent-rules-books`, com referência conceitual a `Implementing Domain-Driven Design`, de Vaughn Vernon. Consulte `.agents/skills/THIRD_PARTY_NOTICES.md`.
