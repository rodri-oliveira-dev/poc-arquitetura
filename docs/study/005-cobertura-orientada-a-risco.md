# Estudo 005 - Cobertura orientada a risco

## Leitura da Product Owner

### Contexto

O projeto possui gate minimo de cobertura e documentacao de testes. O estudo proposto e priorizar testes por risco, nao apenas por percentual.

### Objetivo de negocio

Identificar onde testes adicionais aumentariam mais a confianca no comportamento do sistema.

### Historia de usuario

Como mantenedor do repositorio, quero um relatorio de cobertura orientado a risco para escolher proximos testes com mais valor.

### Criterios de aceite

- Criar relatorio em `docs/reports/coverage-risk-analysis.md`.
- Identificar ate cinco pontos prioritarios.
- Considerar cobertura, complexidade e criticidade funcional quando possivel.
- Nao alterar testes neste primeiro estudo.

## Leitura do Arquiteto

### Abordagem tecnica

Este estudo evita alterar testes apenas para aumentar numero. O foco e encontrar hotspots reais e transformar cada teste recomendado em PR separado.

### Conceitos praticados

- Analise de cobertura.
- CRAP score.
- Testes de unidade e integracao.
- Hotspots de risco.
- Qualidade de asserts.

### Escopo sugerido de PR

Somente relatorio. Depois, cada teste priorizado pode virar uma issue ou PR pequeno.

### Classificacao

Util, mas opcional.
