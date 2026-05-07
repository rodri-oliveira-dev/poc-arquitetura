# ADR-0053: Lock transacional por chave no BalanceService

## Status
Aceito

## Data
2026-05-07

## Contexto
O `BalanceService` consome `LedgerEntryCreated.v1` e atualiza a projecao `daily_balances` de forma idempotente usando `processed_events`.

Antes desta decisao, o handler registrava o evento processado, carregava ou criava `DailyBalance`, aplicava a regra de dominio em memoria e salvava pelo EF Core. O repositorio usava uma consulta comum por `merchant_id`, `date` e `currency`, sem lock pessimista, upsert acumulativo atomico, concurrency token ou retry transacional.

Com dois eventos distintos para o mesmo merchant/data/moeda processados em paralelo, as duas transacoes podiam registrar linhas em `processed_events`, mas competir na mesma linha de `daily_balances`. O resultado possivel era perda de atualizacao ou corrida na criacao do primeiro saldo do dia.

O banco oficial do servico e PostgreSQL, conforme ADR-0007, e a integracao Ledger -> Balance e at-least-once, conforme ADR-0003.

## Decisao
Adotar lock transacional do PostgreSQL por chave logica de saldo antes de carregar ou criar `DailyBalance`.

O `ApplyLedgerEntryCreatedHandler` continua abrindo uma transacao curta e registrando idempotencia em `processed_events` primeiro. Quando o evento e novo, o repositorio de `DailyBalance` executa `pg_advisory_xact_lock(hashtextextended(...))` para a chave:

- `merchantId`;
- `date`;
- `currency`.

Depois do lock, o handler carrega ou cria o saldo diario, aplica a regra de dominio existente e salva `processed_events` e `daily_balances` na mesma transacao.

O lock e especifico de PostgreSQL e fica encapsulado em `BalanceService.Infrastructure`. Em providers nao PostgreSQL usados por testes leves, o metodo de lock e no-op.

## Consequencias

### Beneficios
- Elimina lost update para eventos distintos do mesmo merchant/data/moeda.
- Torna segura a criacao concorrente do primeiro `daily_balance` do dia.
- Preserva idempotencia por `processed_events` para duplicatas de Kafka.
- Mantem a regra financeira em `DailyBalance.Apply`, sem mover regra de negocio para SQL.
- Evita lock global e serializa apenas a chave logica afetada.
- Mantem `processed_events` e `daily_balances` consistentes na mesma transacao.

### Trade-offs / custos
- A solucao depende explicitamente de PostgreSQL.
- Eventos concorrentes para a mesma chave de saldo passam a esperar uns pelos outros.
- A estrategia exige que qualquer novo fluxo de escrita de `daily_balances` use a mesma disciplina de lock.
- Testes que validam concorrencia real precisam de PostgreSQL real; EF InMemory nao valida esse comportamento.

## Alternativas consideradas

1. `SELECT ... FOR UPDATE` na linha de `daily_balances`.
   - Rejeitada como solucao unica porque nao protege a corrida de criacao quando a linha ainda nao existe.

2. Upsert acumulativo atomico em banco.
   - Defensavel, mas rejeitado nesta etapa porque duplicaria a regra de acumulacao CREDIT/DEBIT em SQL e aumentaria acoplamento da regra de dominio com infraestrutura.

3. Concurrency token com retry.
   - Defensavel, mas exigiria nova coluna ou token de concorrencia, migration e politica de retry. Para a POC, o lock por chave reduz escopo e cobre tambem a criacao inicial.

4. Serializar todo o consumer.
   - Rejeitada porque reduziria throughput global e criaria gargalo desnecessario.

## Impacto nos testes
- Teste unitario do handler valida que eventos duplicados continuam sem alterar saldo.
- Teste unitario do handler valida que eventos novos usam o lock antes de carregar/criar o saldo.
- Teste de integracao com Testcontainers PostgreSQL valida dois eventos distintos concorrentes para o mesmo merchant/data/moeda, incluindo criacao concorrente do primeiro saldo, dois `processed_events` persistidos e saldo final acumulado.

## Impacto operacional
- Nao ha mudanca em contrato HTTP, contrato Kafka, topicos, portas ou variaveis de ambiente.
- Ambientes que executarem a suite de integracao com PostgreSQL real precisam de Docker ou runtime compativel para Testcontainers.
