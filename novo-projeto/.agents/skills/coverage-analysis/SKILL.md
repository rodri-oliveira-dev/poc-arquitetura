---
name: coverage-analysis
description: Use esta skill para analisar cobertura de testes, identificar gaps relevantes, hotspots e prioridades de teste. Não use para inflar percentual, reduzir thresholds ou criar testes sem asserts significativos.
license: MIT
---

# Objetivo

Usar cobertura como sinal de risco, não como objetivo isolado. Cobertura mostra o que foi executado, mas não garante que o comportamento esteja corretamente validado.

## Quando usar

- Análise de coverage, gaps ou hotspots.
- Decisão sobre onde adicionar testes primeiro.
- Código complexo com pouca cobertura.
- Preparação para refatoração arriscada.
- Revisão de um gate de cobertura.

## Classificação de risco

### Alto

- invariantes de domínio;
- concorrência de agenda;
- transações;
- autorização;
- idempotência;
- processamento temporal;
- persistência e migrations;
- integrações externas;
- recuperação de falhas.

### Médio

- casos de uso;
- validações relevantes;
- mapeamentos de contratos públicos;
- comportamento transversal.

### Baixo

- DTOs simples;
- configuração declarativa;
- glue code trivial;
- código gerado;
- migrations geradas.

## Processo

1. Leia a estratégia de testes e o relatório disponível.
2. Identifique código público, crítico, complexo e frequentemente alterado.
3. Separe cobertura baixa aceitável de cobertura baixa arriscada.
4. Procure cobertura superficial, como execução sem assert relevante.
5. Priorize testes por comportamento e risco.
6. Para cada gap sugerido, indique:
   - comportamento;
   - cenário;
   - motivo;
   - nível de teste adequado.
7. Gere cobertura com os comandos oficiais do projeto.
8. Não instale ferramenta nova quando Coverlet e relatórios existentes forem suficientes.

## Comando base

```bash
dotnet test ./<Solution>.slnx \
  --configuration Release \
  --settings ./coverlet.runsettings \
  --collect "XPlat Code Coverage"
```

## Restrições

- Não alterar testes somente para aumentar percentual.
- Não aceitar teste sem assert significativo.
- Não excluir código importante do relatório.
- Não reduzir threshold para contornar falha sem decisão explícita.
- Não substituir análise de risco por ranking numérico.

## Saída esperada

- diagnóstico objetivo;
- hotspots priorizados;
- gaps aceitáveis e perigosos;
- testes recomendados por comportamento;
- validações executadas;
- limitações do relatório.
