---
name: optimizing-ef-core-queries
description: Use esta skill para diagnosticar e otimizar consultas EF Core com PostgreSQL/Npgsql, incluindo N+1, tracking, projeções, materialização, paginação, índices e SQL gerado. Não use para trocar a tecnologia de persistência ou adicionar cache sem diagnóstico.
license: MIT
---

# Objetivo

Corrigir problemas reais de consulta e persistência sem introduzir abstrações, cache ou otimizações prematuras.

## Quando usar

- Query lenta ou com alto consumo de banco.
- Risco de N+1.
- `Include` excessivo.
- Tracking desnecessário.
- Materialização antes de filtros, ordenação ou paginação.
- Retorno de volume grande.
- LINQ gerando SQL inadequado.
- Revisão de índices, mappings ou projections.

## Processo

1. Identifique a consulta exata, o caso de uso e o volume esperado.
2. Leia o `DbContext`, mapping, repository e teste relacionados.
3. Verifique:
   - tracking;
   - N+1;
   - `Include`;
   - projeção;
   - materialização prematura;
   - filtro e paginação;
   - cardinalidade;
   - índice;
   - constraint;
   - plano de execução.
4. Inspecione o SQL com `ToQueryString()` quando isso não expuser dados sensíveis.
5. Use `AsNoTracking()` em leituras sem alteração.
6. Use `Select` para buscar somente os campos necessários.
7. Termine filtros, ordenação e paginação antes de `ToListAsync()` ou equivalente.
8. Use paginação, streaming ou lotes para coleções grandes.
9. Avalie `EXPLAIN (ANALYZE, BUFFERS)` em ambiente apropriado quando o gargalo depender do plano real.
10. Se índice, constraint ou tipo de coluna mudar, crie migration e valide impacto.
11. Execute teste próximo e teste de integração com PostgreSQL quando o comportamento do provider for relevante.

## Cuidados com agenda

Para consultas de disponibilidade e conflito de horários:

- filtre por janela temporal no banco;
- não carregue a agenda inteira para calcular disponibilidade em memória;
- considere índices por profissional, recurso, início, término e status;
- valide sobreposição com semântica clara de intervalos;
- trate concorrência no banco, não apenas na consulta anterior à gravação;
- meça antes de criar cache.

## Segurança

- Não habilite `EnableSensitiveDataLogging` como padrão.
- Não registre parâmetros sensíveis, connection strings ou dados pessoais.
- Não concatene entrada externa em SQL.
- Não execute `EXPLAIN ANALYZE` em produção sem autorização.

## Restrições

- Não adicione `Version=` em `PackageReference`.
- Não crie Generic Repository, Specification ou compiled query apenas por padrão.
- Não troque PostgreSQL/Npgsql sem decisão explícita.
- Não use cache antes de confirmar a causa.
- Não trate EF InMemory como evidência de comportamento SQL real.

## Saída esperada

- causa provável;
- menor ajuste seguro;
- impacto esperado;
- evidência ou medição;
- validações executadas;
- riscos restantes.
