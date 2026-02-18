# ADR-0000: Registrar decisões arquiteturais com ADRs

## Status
Substituído (consolidado em `docs/adrs/README.md`)

## Data
2026-02-16

## Contexto
A PoC tem múltiplas decisões arquiteturais (microserviços, integração assíncrona, autenticação, padrões de repositório). Sem um histórico objetivo, o racional se perde e aumenta o custo de evolução.

## Decisão
Adotar Architecture Decision Records (ADRs) em Markdown sob `docs/adrs/`, com numeração sequencial e um padrão mínimo de seções:
- Título
- Status
- Contexto
- Decisão
- Consequências

## Motivo da substituição
O repositório já possui um **índice vivo** e regras de uso em `docs/adrs/README.md` (incluindo critérios de status e o propósito de ADRs vs pontos de melhoria). Para reduzir redundância, este ADR foi mantido apenas como histórico.

## Consequências
- Facilita onboarding e manutenção do racional.
- Reduz discussões repetidas e divergência de entendimento.
- Exige disciplina para criar/atualizar ADR quando houver mudança relevante.

## Alternativas consideradas
- Documentar apenas no README: tende a virar “parede de texto” e perder histórico.
- Wiki externa: cria drift e dificulta versionamento junto do código.
