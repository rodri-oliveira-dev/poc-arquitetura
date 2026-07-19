---
name: ddd-modeling-vernon
description: Use esta skill para discovery de domínio, Event Storming, bounded contexts, subdomínios, context map, aggregates, eventos, glossário, workflows e validação de cenários. Use antes de mudanças grandes ou quando houver ambiguidade de linguagem e fronteiras. Não use para correções técnicas pequenas.
---

# Objetivo

Transformar entendimento de negócio em artefatos úteis para código, testes, contratos e decisões arquiteturais, sem criar documentação abstrata demais.

## Quando usar

- Descoberta de novos fluxos do petshop.
- Definição ou revisão de bounded contexts.
- Modelagem de agenda, disponibilidade, atendimento, tutor, pet, profissional, recurso ou cobrança.
- Criação ou revisão de aggregates.
- Identificação de comandos, eventos, invariantes e políticas.
- Decisão entre CRUD simples e modelo de domínio rico.
- Preparação de ADR com impacto em domínio ou consistência.

## Artefatos recomendados

Quando a tarefa pedir registro persistente, use `docs/domain/`:

- `discovery.md`;
- `event-storming.md`;
- `bounded-contexts.md`;
- `context-map.md`;
- `aggregates.md`;
- `domain-events.md`;
- `workflows.md`;
- `glossary.md`;
- `validation.md`;
- `modeling-debt.md`.

Não crie todos automaticamente. Produza apenas os necessários.

## Processo

1. Entenda o problema de negócio, usuários, riscos e resultado esperado.
2. Classifique o subdomínio como core, supporting ou generic.
3. Mapeie eventos como fatos no passado.
4. Identifique comandos, atores, políticas e sistemas externos.
5. Defina bounded contexts pela linguagem e responsabilidade, não pelas tabelas.
6. Registre relações no context map.
7. Desenhe aggregates por invariantes e limites de consistência.
8. Diferencie eventos de domínio e integração.
9. Modele workflows com entrada, validação, decisão, persistência, efeitos e erros.
10. Simule cenários de sucesso, conflito, cancelamento, atraso, duplicidade e concorrência.
11. Registre dúvidas e dívida de modelagem.

## Perguntas de challenge

- Esse termo tem o mesmo significado em todos os contextos?
- Essa regra pertence ao aggregate ou ao caso de uso?
- Essa transação precisa alterar vários aggregates?
- O evento é um fato de negócio ou uma notificação técnica?
- O conceito pertence ao domínio ou é detalhe de API, banco ou fornecedor?
- Esse CRUD realmente precisa de DDD tático?
- Como o modelo impede dois agendamentos para o mesmo profissional ou recurso?
- O que acontece quando duração, profissional ou recurso mudam após a reserva?

## Regras de qualidade

- Modele o mínimo suficiente para orientar implementação segura.
- Prefira termos do negócio aos termos técnicos.
- Registre suposições e incertezas.
- Não compartilhe modelo de domínio entre contextos por conveniência.
- Não trate agenda, disponibilidade, atendimento e cobrança como o mesmo conceito.
- Toda decisão de modelagem deve ajudar código, teste, contrato ou operação.

## Fontes e atribuição

Adaptação operacional em português inspirada em materiais MIT de `tango238/distill-ddd` e `ciembor/agent-rules-books`, com referências conceituais a Vaughn Vernon e Scott Wlaschin. Consulte `.agents/skills/THIRD_PARTY_NOTICES.md`.
