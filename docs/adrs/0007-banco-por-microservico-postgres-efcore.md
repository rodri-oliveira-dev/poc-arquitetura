# ADR-0007: Banco por microserviço (PostgreSQL) com EF Core

## Status
Aceito

## Data
2026-02-17

## Contexto
O repositório contém múltiplos microserviços (Ledger, Balance e Auth), com necessidades e modelos diferentes.

Se todos compartilhassem o mesmo banco/esquema, teríamos:

- acoplamento de deploy (um serviço “quebra” migrations dos outros);
- evolução mais lenta do modelo de dados;
- risco de acesso direto de um serviço aos dados do outro (quebra de boundary);
- dificuldade de escalar/otimizar por carga (write-heavy vs read-heavy).

Além disso, a PoC usa EF Core como ORM e precisa de migrations reprodutíveis.

## Decisão
Adotar o padrão **database-per-service**:

- cada microserviço possui seu **PostgreSQL** (ou database dedicado), connection string e migrations;
- persistência via **Entity Framework Core**, com `DbContext` no projeto `*.Infrastructure`.

No compose local, isso se materializa em dois Postgres separados (Ledger e Balance). O Auth mantém persistência apenas de chave RSA em arquivo/volume (PoC).

## Justificativa: escolha de banco (PostgreSQL) vs alternativas
A escolha por **PostgreSQL + EF Core** faz sentido para esta PoC por:

- **Transações e consistência**: o Ledger precisa de garantias transacionais para gravar `ledger_entries` + `outbox_messages` na mesma transação.
- **Funcionalidades específicas usadas**: o repositório implementa *claim* do outbox com `FOR UPDATE SKIP LOCKED` (vide `OutboxMessageRepository`), recurso suportado muito bem no Postgres.
- **Custo operacional e previsibilidade**: Postgres é fácil de rodar em compose e é amplamente conhecido.
- **Modelo relacional adequado** para projeções (`daily_balances`) e queries agregadas previsíveis.

Alternativas típicas e por que não foram escolhidas aqui:

1) **SQL Server**
   - Prós: ecossistema .NET forte.
   - Contras: equivalentes a `SKIP LOCKED` existem, mas exigiriam adaptação e aumentariam o escopo.

2) **MySQL/MariaDB**
   - Prós: popular.
   - Contras: diferenças de locking/isolamento e suporte a padrões de concorrência do outbox variam; o repo já depende de Postgres.

3) **NoSQL (MongoDB/DynamoDB)**
   - Prós: flexibilidade de schema.
   - Contras: transação + outbox e projeções agregadas ficariam mais complexas (ou exigiriam desenho diferente).

> TODO: se a PoC evoluir para produção, documentar requisitos não-funcionais (RPO/RTO, HA, backups, particionamento, e estratégia de migração entre bancos).

## Consequências

### Benefícios
- Autonomia de evolução e deploy por serviço.
- Reduz risco de “join cross-service” e reforça boundaries.
- Melhor alinhamento com a separação Ledger (transacional) vs Balance (projeção).

### Trade-offs / custos
- Mais componentes para operar localmente (vários bancos).
- Queries cross-context viram **integração** (eventos/APIs), não SQL.
- Necessidade de estratégia de consistência eventual e reprocessamento (já endereçada via eventos/outbox).

## Alternativas consideradas

1) **Um banco compartilhado com schemas por serviço**
   - Prós: menos infra.
   - Contras: acoplamento ainda existe; risco de acessos indevidos; deploy/migrations continuam acoplados.

2) **Um banco único**
   - Prós: simplicidade máxima.
   - Contras: conflita com o objetivo de demonstrar boundaries e evolução independente.
