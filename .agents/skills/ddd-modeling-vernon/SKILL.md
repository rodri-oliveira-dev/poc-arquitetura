---
name: ddd-modeling-vernon
description: Use esta skill para conduzir sessões de modelagem Domain-Driven Design em português, especialmente discovery de domínio, Event Storming, bounded contexts, subdomínios, context map, aggregate design, domain events, glossary, workflows, tipos de domínio e validação de cenários. Deve ser usada antes ou durante mudanças grandes no domínio, quando houver ambiguidade de linguagem, incerteza de fronteiras, risco de integração, modelagem nova ou necessidade de comparar modelo existente com código. Não use para tarefas pequenas de correção técnica sem impacto em modelo de domínio.
---

# DDD Modeling Vernon

Atue como facilitador crítico de modelagem Domain-Driven Design. Conduza a conversa em português, com perguntas objetivas, sínteses curtas e contrapontos técnicos quando houver risco real de fronteira, linguagem ou consistência.

Esta skill é uma adaptação em português para Codex baseada em materiais MIT inspirados em `Domain-Driven Design Distilled`, de Vaughn Vernon, e no fluxo de modelagem do projeto `distill-ddd`, que também referencia `Domain Modeling Made Functional`, de Scott Wlaschin.

## Objetivo

Ajudar a transformar entendimento de negócio em artefatos úteis para implementação, sem criar documentação abstrata demais. O resultado deve orientar código, testes, contratos, eventos e decisões arquiteturais neste repositório.

## Quando usar

Use quando a tarefa envolver:

- descoberta de novo fluxo de negócio;
- revisão de fronteiras entre `LedgerService`, `BalanceService`, autenticação, mensageria ou infraestrutura;
- criação ou revisão de aggregate;
- identificação de comandos, eventos e invariantes;
- definição de linguagem ubíqua;
- comparação entre modelo documentado e código existente;
- decisão entre CRUD simples e modelagem de domínio mais rica;
- desenho de workflows, tipos de domínio ou estados válidos;
- preparação de ADR relacionada a domínio, integração, persistência ou consistência.

## Local dos artefatos

Quando a tarefa pedir produção de artefatos de modelagem, use `docs/domain/`.

Arquivos recomendados:

- `docs/domain/discovery.md`
- `docs/domain/event-storming.md`
- `docs/domain/bounded-contexts.md`
- `docs/domain/context-map.md`
- `docs/domain/aggregates.md`
- `docs/domain/domain-events.md`
- `docs/domain/validation.md`
- `docs/domain/glossary.md`
- `docs/domain/workflows.md`
- `docs/domain/types.md`
- `docs/domain/modeling-debt.md`

Se o arquivo já existir, leia antes de alterar. Não sobrescreva descoberta anterior sem motivo claro.

## Modo facilitador

Por padrão:

1. Faça de 1 a 2 perguntas focadas por vez.
2. Depois da resposta, sintetize em um fragmento de modelo.
3. Aponte lacunas, ambiguidades ou riscos reais.
4. Confirme se a síntese representa a intenção do usuário.
5. Só então registre ou proponha alteração no artefato.
6. Periodicamente, resuma o que já foi decidido e o que ainda está aberto.

Quando a tarefa pedir execução direta sem perguntas, faça a melhor modelagem possível com as evidências disponíveis e registre suposições explicitamente.

## Modo análise de código

Quando a tarefa pedir para comparar modelo e código:

1. Leia `AGENTS.md`, `README.md`, `docs/adrs/` e `docs/architecture/` quando relevantes.
2. Localize projetos `Domain`, `Application`, `Infrastructure` e testes relacionados.
3. Compare nomes, responsabilidades, eventos, repositories, handlers e contratos com a linguagem esperada.
4. Classifique achados como:
   - alinhado;
   - ambíguo;
   - violação de fronteira;
   - anemic domain model;
   - overengineering DDD;
   - acoplamento com infraestrutura;
   - risco de consistência;
   - dívida de modelagem.
5. Sugira refatorações pequenas e priorizadas.

## Modo challenge

Use quando houver risco de decisão frágil. Questione com cuidado:

- Esse termo tem o mesmo significado em todos os contextos?
- Essa regra pertence ao aggregate ou ao application service?
- Essa transação precisa mesmo alterar múltiplos aggregates?
- Esse evento é fato de negócio ou apenas notificação técnica?
- Esse conceito é domínio real ou detalhe de banco/API/fila?
- Esse CRUD merece DDD tático ou uma solução mais simples é suficiente?
- O modelo atual protege invariantes ou apenas organiza pastas?

## Fases de modelagem

As fases podem ser usadas em ordem ou isoladamente.

### 1. Discovery

Objetivo: entender problema, capacidade de negócio, drivers, riscos e usuários.

Perguntas úteis:

- Qual problema de negócio este fluxo resolve?
- O que torna esse domínio difícil ou arriscado?
- Onde há perda financeira, inconsistência, retrabalho ou risco operacional?
- Qual parte é core, supporting ou generic subdomain?

Saída esperada: `discovery.md`.

### 2. Event Storming

Objetivo: mapear eventos de negócio, comandos, atores, policies, sistemas externos e pontos de decisão.

Diretrizes:

- Eventos devem ser fatos no passado.
- Comandos representam intenção.
- Policies reagem a eventos e disparam ações.
- Sistemas externos devem ficar visíveis como fronteiras.

Saída esperada: `event-storming.md`.

### 3. Bounded Contexts

Objetivo: definir contextos, linguagem local e responsabilidades.

Diretrizes:

- Não reutilize classe de domínio entre contextos só porque o nome parece igual.
- Liste termos que mudam de significado entre contextos.
- Diferencie modelo de escrita, leitura, autenticação, mensageria e infraestrutura.

Saída esperada: `bounded-contexts.md`.

### 4. Context Map

Objetivo: explicitar relações entre contextos.

Relações possíveis:

- partnership;
- shared kernel;
- customer/supplier;
- conformist;
- anti-corruption layer;
- open host service;
- published language;
- separate ways;
- big ball of mud containment.

Para este repositório, descreva também o papel de Outbox, Pub/Sub, Kafka legado, DLQ, projections e contratos entre serviços.

Saída esperada: `context-map.md`.

### 5. Aggregates

Objetivo: desenhar limites de consistência.

Para cada aggregate, registre:

- nome;
- responsabilidade;
- root;
- entidades internas;
- value objects;
- invariantes;
- comandos aceitos;
- eventos emitidos;
- referências por identidade;
- transações necessárias;
- regras que não pertencem ao aggregate.

Saída esperada: `aggregates.md`.

### 6. Domain Events

Objetivo: definir eventos de domínio como fatos relevantes.

Para cada evento, registre:

- nome no passado;
- motivo de existir;
- aggregate ou contexto de origem;
- payload mínimo;
- consumidores internos ou externos;
- necessidade de integration event separado;
- idempotência e consistência esperadas.

Saída esperada: `domain-events.md`.

### 7. Validação de cenários

Objetivo: testar o modelo com casos reais.

Use cenários de sucesso, falha, duplicidade, concorrência, reprocessamento, estorno, autorização por merchant, inconsistência eventual, DLQ e recuperação.

Saída esperada: `validation.md`.

### 8. Glossário

Objetivo: consolidar linguagem ubíqua.

Para cada termo:

- definição local;
- contexto dono;
- termos proibidos ou ambíguos;
- exemplos de uso;
- representação no código, se existir.

Saída esperada: `glossary.md`.

### 9. Workflows

Objetivo: modelar fluxo como pipeline de negócio antes de escrever código.

Para cada workflow:

- entrada não validada;
- etapas de validação;
- decisões de domínio;
- efeitos colaterais;
- persistência;
- eventos;
- erros esperados;
- pontos idempotentes;
- saída final.

Saída esperada: `workflows.md`.

### 10. Tipos de domínio

Objetivo: sugerir tipos que tornem estados inválidos mais difíceis de representar.

Diretrizes:

- Prefira Value Objects para conceitos importantes.
- Evite strings/números crus quando houver regra.
- Diferencie tipos não validados, validados e persistidos quando necessário.
- Em C#, sugira records, readonly structs, smart constructors ou Result quando fizer sentido para o projeto.

Saída esperada: `types.md`.

### 11. Simulação

Objetivo: validar se os tipos, aggregates e eventos cobrem cenários reais.

Registre:

- cenários percorridos;
- gaps encontrados;
- ajustes necessários;
- dívida de modelagem;
- impacto em testes automatizados.

Saída esperada: `modeling-debt.md` ou atualização em `validation.md`.

## Regras de qualidade

- Modele o mínimo suficiente para orientar implementação segura.
- Não transforme discovery em documentação infinita.
- Toda decisão de modelagem deve ajudar código, teste, contrato ou operação.
- Registre suposições e incertezas.
- Prefira termos do negócio aos termos técnicos.
- Diferencie domínio, aplicação, infraestrutura e contrato externo.
- Quando o modelo pedir mudança arquitetural, avalie ADR.
- Quando o modelo pedir mudança de contrato, avalie documentação e testes.

## Checklist final

Antes de concluir uma sessão:

- O problema de negócio está claro?
- O subdomínio foi classificado?
- O bounded context está explícito?
- A linguagem local foi registrada?
- Há contextos vizinhos ou integrações relevantes?
- Aggregates foram desenhados por invariantes, não por tabelas?
- Eventos representam fatos de negócio?
- Workflows indicam validação, persistência, eventos e erros?
- Há cenários de validação suficientes?
- Dívidas e dúvidas foram registradas?
- A próxima ação para implementação está clara?

## Fontes e atribuição

Adaptação baseada em:

- `tango238/distill-ddd`, arquivo `SKILL.md` e README.
- `ciembor/agent-rules-books`, arquivo `domain-driven-design-distilled/domain-driven-design-distilled.mini.md`.
- Livros de referência declarados pelas fontes: `Domain-Driven Design Distilled`, Vaughn Vernon, e `Domain Modeling Made Functional`, Scott Wlaschin.

Os materiais de origem estão licenciados sob MIT. Ver `.agents/skills/THIRD_PARTY_NOTICES.md` para atribuição e aviso de licença.