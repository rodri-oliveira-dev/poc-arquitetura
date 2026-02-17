# ADR-0003: Banco por microserviço (PostgreSQL) com EF Core

## Status
Aceito

## Data
2026-02-16

## Contexto
Ledger e Balance têm responsabilidades distintas e modelos de dados diferentes. O README define “um banco por microserviço” e EF Core + PostgreSQL.

## Decisão
Manter dois bancos PostgreSQL:
- ledger: `appdb`
- balance: `dbBalance`

Acesso via EF Core (DbContext por serviço) e migrations no projeto Infrastructure.

## Consequências
- Independência de schema e deploy de migrations por serviço.
- Evita “acoplamento por banco” e reduz risco de mudanças cruzadas.
- Impõe integração por eventos (não por join cross-service).
- Requer estratégia clara de migração/backup por serviço.

## Alternativas consideradas
- Banco compartilhado: simplifica consultas, mas acopla deploy e quebra autonomia.
- Event store: mais robusto para auditoria/event sourcing, porém foge do escopo PoC.
