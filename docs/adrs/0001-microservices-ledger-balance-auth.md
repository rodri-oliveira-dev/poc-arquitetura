# ADR-0001: Adotar decomposição em microserviços (Ledger, Balance e Auth)

## Status
Aceito

## Data
2026-02-16

## Contexto
O domínio pede separação entre:
- Escrita de lançamentos (Ledger)
- Leitura de consolidado (Balance)
- Emissão de tokens (Auth)

Além disso, há requisito não-funcional explícito: Ledger não deve ficar indisponível se Balance cair.

## Decisão
Implementar três serviços:
- LedgerService.Api (write + outbox)
- BalanceService.Api (read + consumer + projeção)
- Auth.Api (emissão de JWT e JWKS)

## Consequências
- Isolamento de falhas: Balance pode cair sem derrubar o Ledger (via Kafka/outbox).
- Permite escalar leitura (Balance) e escrita (Ledger) independentemente.
- Aumenta custo operacional (mais deploys, configs, observabilidade e governança).
- Auth.Api vira mais um componente crítico (ver ADR-0100 para evolução).

## Alternativas consideradas
- Monólito: reduziria custo operacional, mas mistura write/read e piora isolamento.
- Dois serviços (Ledger e Balance) com autenticação embutida: simplifica, porém acopla segurança a serviços de negócio.
