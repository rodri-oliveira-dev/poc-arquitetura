# ADR-0001: Separar Ledger (escrita) e Balance (leitura) com projeção assíncrona

## Status
Aceito

## Data
2026-02-17

## Contexto
O domínio da PoC envolve:

- **Escrita** de lançamentos (alta taxa de escrita, regras de domínio, idempotência).
- **Leitura** de consolidados (consultas por dia e por período, com resposta rápida e formato orientado a leitura).

Uma única API/banco com a mesma modelagem para escrita e leitura tende a:

- degradar performance de leitura (JOINs/aggregations frequentes);
- acoplar a evolução do modelo transacional ao modelo de consulta;
- aumentar a complexidade de concorrência/locking.

O repositório já possui dois serviços (`LedgerService.Api` e `BalanceService.Api`), cada um com seu banco, e a leitura é alimentada por eventos via Kafka.

## Decisão
Adotar uma separação de responsabilidades no estilo **CQRS “básico”**:

- `LedgerService` como **fonte de verdade** para lançamentos (write model);
- `BalanceService` como **read model**, mantendo uma **projeção** (`daily_balances`) atualizada de forma **assíncrona** a partir de eventos do Ledger.

Cada microserviço mantém:

- **banco dedicado** (PostgreSQL) e migrations independentes;
- contrato HTTP (DTOs) voltado ao seu caso de uso.

## Consequências

### Benefícios
- Leituras de consolidado ficam simples e baratas (tabela/projeção já agregada).
- Evolução independente: regras de escrita podem mudar sem forçar remodelagem imediata do read model.
- Permite escalar leitura e escrita separadamente.

### Trade-offs / custos
- **Consistência eventual**: o Balance pode estar defasado por alguns segundos.
- Maior superfície operacional (dois serviços + mensageria + duas bases).
- Necessidade de lidar com reprocessamento/idempotência no consumo de eventos.

## Alternativas consideradas

1) **Monólito** (um serviço + um banco)
   - Prós: simplicidade operacional.
   - Contras: acoplamento de modelos, custo de consultas agregadas, evolução mais lenta.

2) **Views/materialized views** no mesmo banco
   - Prós: evita mensageria.
   - Contras: mantém acoplamento e não resolve separação/escala por serviço.

3) **Síncrono por request** (Balance consulta Ledger para compor resposta)
   - Prós: consistência forte.
   - Contras: latência e acoplamento runtime; piora disponibilidade (cascata).
