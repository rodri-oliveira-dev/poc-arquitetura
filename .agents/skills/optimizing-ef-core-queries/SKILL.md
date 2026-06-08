---
name: optimizing-ef-core-queries
description: Use esta skill para diagnosticar e otimizar queries EF Core neste repositorio .NET com PostgreSQL/Npgsql, incluindo N+1, tracking, projecoes, materializacao prematura, filtros, indices, SQL gerado, alto consumo de banco e armadilhas de performance. Nao use para criar nova camada de acesso a dados ou para trocar EF Core por outra tecnologia sem decisao explicita.
license: MIT
---

# Objetivo

Orientar otimizacao pragmatica de queries EF Core nos servicos deste repositorio, respeitando PostgreSQL, Npgsql, Clean Architecture, Central Package Management e as decisoes registradas em ADRs.

Esta skill deve ajudar o Codex a corrigir problemas reais de performance sem introduzir cache, abstrações ou refatoracoes amplas como resposta automatica.

# Quando usar

- Query EF Core lenta, com alto consumo de CPU/IO no banco.
- Risco de N+1 queries.
- Uso incorreto de `Include`, tracking ou materializacao.
- Retorno de volume grande sem paginacao, filtro ou projecao.
- LINQ gerando SQL inadequado para PostgreSQL.
- Dificuldade de entender se o problema esta no LINQ, no indice, no schema, no volume ou na infraestrutura.
- Revisao de PR que altera consultas, repositories, DbContext, mappings ou projections.

# Quando nao usar

- Dapper, ADO.NET puro ou SQL manual sem EF Core.
- Problema claramente causado apenas por infraestrutura externa, rede, pool ou banco indisponivel.
- Criacao de data access layer do zero.
- Ajuste de modelo de dominio sem problema de query.
- Aplicar cache antes de entender a causa da lentidao.

# Regras obrigatorias

- Nao adicione `Version=` em `PackageReference`; o repositorio usa Central Package Management.
- Nao habilite `EnableSensitiveDataLogging` fora de contexto local/dev e nunca deixe isso como configuracao padrao.
- Nao registre parametros sensiveis, connection strings, tokens ou payloads completos.
- Nao troque provider, banco, schema, indice ou tipo de coluna sem avaliar migration, ADR e impacto em dados.
- Nao use exemplos de SQL Server como referencia direta. O banco alvo principal deste projeto e PostgreSQL/Npgsql.
- Nao crie repository, specification, cache ou compiled query apenas por padrao. Justifique pelo ganho real.

# Processo

1. Leia `AGENTS.md`, ADRs de persistencia, mappings EF Core, DbContext, repository concreto e testes relacionados.
2. Identifique a query exata, volume esperado, caminho de chamada e contrato afetado.
3. Verifique se o problema e de tracking, N+1, `Include`, materializacao prematura, falta de filtro, falta de paginacao, projection ausente, indice, cardinalidade ou plano do banco.
4. Gere ou inspecione SQL com `ToQueryString()` quando fizer sentido e nao expuser dados sensiveis.
5. Prefira `AsNoTracking()` em leituras sem alteracao.
6. Prefira `Select` com projecao quando o endpoint/caso de uso nao precisa da entidade completa.
7. Evite `ToList`, `ToArray`, `AsEnumerable` ou materializacao equivalente antes de compor filtros, ordenacao e paginacao.
8. Avalie `Include` apenas quando realmente precisar do grafo de objetos. Para leitura, muitas vezes projection e melhor.
9. Para colecoes grandes, use paginacao, filtros seletivos, streaming ou processamento em lotes conforme o caso.
10. Se o gargalo parecer banco/indice, registre a hipotese e proponha validacao com `EXPLAIN (ANALYZE, BUFFERS)` em ambiente apropriado, sem executar contra producao sem autorizacao.
11. Se a mudanca alterar schema, indice, constraint ou tipo de coluna, avalie migration e documentacao.
12. Valide com teste proximo, build ou teste de integracao proporcional ao impacto.

# Exemplos seguros

Para inspecionar SQL gerado em diagnostico local:

```csharp
var sql = query.ToQueryString();
```

Para habilitar logs de comandos EF Core em ambiente local, prefira configuracao controlada e sem dados sensiveis:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Use `EnableSensitiveDataLogging()` apenas temporariamente em ambiente local, com consciencia de que valores de parametros podem aparecer nos logs.

# Saida esperada

- Diagnostico curto da causa provavel.
- Menor ajuste seguro no LINQ, mapping, indice ou contrato, conforme o problema real.
- Explicacao do impacto esperado.
- Validacoes executadas ou motivo para nao executar.
- Riscos restantes, especialmente quando a confirmacao depende de plano real do PostgreSQL.
