# ADR-0000: Registrar decisões arquiteturais com ADRs

## Status
Aceito

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

## Consequências
- Facilita onboarding e manutenção do racional.
- Reduz discussões repetidas e divergência de entendimento.
- Exige disciplina para criar/atualizar ADR quando houver mudança relevante.

## Alternativas consideradas
- Documentar apenas no README: tende a virar “parede de texto” e perder histórico.
- Wiki externa: cria drift e dificulta versionamento junto do código.
