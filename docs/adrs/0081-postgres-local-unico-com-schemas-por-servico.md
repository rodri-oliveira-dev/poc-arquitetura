# ADR-0081: PostgreSQL local unico com schemas por servico

## Status
Aceito

## Data
2026-06-04

## Contexto
O compose local da POC passou a usar um unico container PostgreSQL (`postgres-db`) para reduzir custo operacional local e simplificar a stack de desenvolvimento. Mesmo com uma instancia unica, Ledger e Balance continuam precisando de isolamento logico para evitar acesso cruzado e para separar credenciais de runtime das credenciais usadas em migrations.

A ADR-0007 permanece como historico da decisao original de banco por microservico. Esta ADR registra a adaptacao local atual: um PostgreSQL compartilhado por container, com boundaries reforcados por schemas e roles.

## Decisao
Usar um unico database local (`appdb`) no container `postgres-db`, com schemas dedicados:

- `ledger`, owned por `ledger_migrator_user`;
- `balance`, owned por `balance_migrator_user`.

Separar credenciais por responsabilidade:

- `ledger_app_user`: runtime do Ledger, com DML no schema `ledger`;
- `ledger_migrator_user`: migrations do Ledger, com ownership/DDL no schema `ledger`;
- `balance_read_user`: runtime read-only do `BalanceService.Api`, com `SELECT` no schema `balance`;
- `balance_write_user`: runtime do `BalanceService.Worker`, com DML no schema `balance`;
- `balance_migrator_user`: migrations do Balance, com ownership/DDL no schema `balance`.

O compose local monta `infra/postgres/init` em `/docker-entrypoint-initdb.d:ro` para criar roles, schemas, grants e default privileges na primeira inicializacao do volume PostgreSQL.

## Consequencias

- A stack local fica mais simples, com um unico container PostgreSQL persistente.
- O isolamento local passa a depender de permissao de schema/role, nao de containers separados.
- Usuarios de runtime nao recebem permissao de `CREATE`, `ALTER` ou `DROP`.
- Migrations devem usar os usuarios migrator correspondentes, nao usuarios de runtime.
- Como scripts de init do PostgreSQL executam apenas quando o volume esta vazio, alteracoes em roles, senhas ou grants exigem recriacao consciente do volume local ou aplicacao manual do SQL.

## Alternativas consideradas

1. Manter dois containers PostgreSQL locais.
   - Preserva isolamento fisico local mais forte, mas aumenta custo operacional e diverge do novo objetivo de simplificar a stack.

2. Usar um unico schema compartilhado.
   - Reduz configuracao, mas enfraquece boundaries e aumenta risco de acesso cruzado entre servicos.

3. Usar um unico usuario de aplicacao.
   - Simplifica connection strings, mas viola separacao de responsabilidades e dificulta validar permissao read-only para o BalanceService.Api.
