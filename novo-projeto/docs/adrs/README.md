# Architecture Decision Records

Este diretório registra decisões arquiteturais relevantes para o novo projeto.

ADRs são históricas. Depois que uma decisão for aceita, não reescreva o arquivo como se a decisão original nunca tivesse existido. Quando a arquitetura mudar, crie uma nova ADR que substitua ou complemente a anterior.

## Decisões

| ADR | Status | Decisão |
| --- | --- | --- |
| [ADR-0001](0001-multitenancy-claim-e-isolamento-por-linha.md) | Aceita | Resolver o tenant pela claim `tenant_id` do token e exigir a coluna `tenant_id` em todas as tabelas de negócio |
| [ADR-0002](0002-library-propagacao-observabilidade.md) | Aceita | Padronizar correlação HTTP e propagação W3C/multitenant em building blocks agnósticos de mensageira |

## Convenção

Use nomes no formato:

```text
NNNN-titulo-curto-em-kebab-case.md
```

Cada ADR deve registrar:

- contexto;
- decisão;
- consequências;
- alternativas consideradas;
- pontos ainda pendentes;
- relação com código, testes ou documentação operacional.
