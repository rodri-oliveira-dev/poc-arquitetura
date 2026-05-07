# ADR-0056: Uso de Testcontainers para testes de integracao com PostgreSQL

## Status
Aceito

## Data
2026-05-07

## Contexto

O repositorio usa PostgreSQL como banco oficial dos servicos Ledger e Balance, com EF Core, migrations e SQL especifico do provider em alguns fluxos de infraestrutura.

Parte dos testes de integracao leves continua usando EF InMemory para validar pipeline HTTP, autenticacao, autorizacao e limites operacionais sem custo de infraestrutura real. Porem, os cenarios que exercitam concorrencia, transacoes, locks, constraints, migrations ou comportamento especifico de PostgreSQL precisam rodar contra PostgreSQL real para evitar divergencia entre teste e producao.

O projeto ja possuia dependencias `Testcontainers` e `Testcontainers.PostgreSql` centralizadas e testes PostgreSQL selecionados, mas a estrategia precisava ficar explicita: sem banco local obrigatorio, sem dependencia de `docker compose` para esses testes e sem portas fixas para o PostgreSQL iniciado pelo Testcontainers.

## Decisao

Padronizar os testes de integracao que dependem de PostgreSQL real para usar Testcontainers.

O PostgreSQL de teste deve ser iniciado automaticamente pelo ciclo de vida das fixtures xUnit, com connection string dinamica obtida do container. Os testes nao devem depender de PostgreSQL instalado localmente, de banco compartilhado, de `docker compose` ou de portas fixas publicadas no host.

As migrations EF Core devem ser aplicadas explicitamente durante a inicializacao da fixture, preservando a decisao de nao aplicar migrations automaticamente no startup das APIs.

Quando os testes compartilharem o mesmo banco da fixture, o isolamento deve ser garantido por limpeza explicita das tabelas afetadas e por collections xUnit especificas com paralelismo desabilitado apenas para os cenarios PostgreSQL correspondentes.

## Consequencias

- Testes selecionados passam a validar PostgreSQL real, provider Npgsql, migrations e comportamento relacional.
- A execucao desses testes exige uma Docker-compatible API acessivel pelo Testcontainers.
- Nao e necessario executar `docker compose up` nem manter PostgreSQL local para os testes migrados.
- O runtime de containers escolhe portas dinamicas, reduzindo conflitos com a stack local.
- Testes leves que nao precisam de PostgreSQL real podem continuar usando InMemory, mocks ou fakes quando isso for coerente com o risco validado.

## Beneficios

- Aumenta a fidelidade dos testes de concorrencia, transacao, locking e persistencia.
- Reduz risco de falhas causadas por banco local compartilhado ou estado residual externo.
- Melhora reprodutibilidade local e em CI.
- Mantem o custo de execucao limitado aos testes que realmente precisam de infraestrutura real.
- Preserva compatibilidade com Windows sem Docker Desktop quando houver Docker-compatible API, como Rancher Desktop com `moby/dockerd`.

## Trade-offs / custos

- A suite passa a depender de runtime de containers corretamente configurado para executar os testes PostgreSQL.
- Testes com container sao mais lentos que testes unitarios ou testes HTTP com InMemory.
- Fixtures compartilhadas exigem disciplina de limpeza de dados e controle localizado de paralelismo.
- Falhas ambientais de Docker-compatible API precisam ser tratadas por configuracao do ambiente, nao por `Skip` ou relaxamento dos testes.

## Alternativas consideradas

1. **Manter banco local ou compose para testes**
   - Rejeitado porque cria dependencia externa manual, risco de porta em conflito e estado residual.

2. **Usar portas fixas no Testcontainers**
   - Rejeitado porque pode conflitar com a stack local ou outro PostgreSQL no host.

3. **Substituir PostgreSQL por SQLite ou EF InMemory em todos os testes**
   - Rejeitado para cenarios PostgreSQL porque o projeto depende de provider Npgsql, migrations, transacoes e SQL especifico.

4. **Criar container por teste**
   - Rejeitado como padrao inicial pelo custo de execucao. A estrategia adotada usa container compartilhado por collection e limpeza explicita dos dados.
