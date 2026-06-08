---
name: coverage-analysis
description: Use esta skill para analisar cobertura de testes neste repositorio .NET, identificar gaps relevantes, hotspots de risco, classes/metodos perigosos de modificar e prioridades de teste. Nao use para inflar cobertura, alterar testes sem validar comportamento ou instalar ferramentas sem necessidade concreta.
license: MIT
---

# Objetivo

Orientar uma analise pragmatica de cobertura para encontrar risco real, nao apenas aumentar percentual.

Cobertura responde o que foi exercitado, mas nao garante qualidade. Use esta skill para priorizar testes em codigo com maior risco de mudanca, complexidade ou criticidade operacional.

# Quando usar

- O usuario mencionar cobertura, coverage, gate, gaps, hotspots, CRAP score ou risco de refatoracao.
- A cobertura estiver travada ou abaixo do gate esperado.
- For necessario decidir onde adicionar testes primeiro.
- Um PR alterar codigo complexo com pouca cobertura.
- Houver duvida se determinada area esta segura para refatorar.

# Quando nao usar

- Escrever testes novos sem analise de cobertura.
- Corrigir falha funcional de teste sem relacao com coverage.
- Rodar testes apenas para validar build.
- Inflar cobertura tocando codigo sem assert significativo.
- Instalar ferramenta global, pacote ou script sem necessidade comprovada.

# Regras obrigatorias

- Nao altere testes apenas para aumentar percentual.
- Nao aceite teste sem assert significativo como melhoria real de cobertura.
- Nao reduza o threshold de cobertura para contornar falha sem instrucao explicita.
- Nao adicione pacote com `Version=`; use Central Package Management.
- Nao instale ferramentas globais se o repositorio ja tiver scripts ou ferramentas locais suficientes.
- Nao substitua analise de risco por ranking puramente numerico.
- Considere complexidade, criticidade, frequencia de mudanca, comportamento publico e impacto operacional.

# Fontes e arquivos relevantes

Consulte quando existirem ou forem relevantes:

- `AGENTS.md`
- `coverlet.runsettings`
- `test.ps1`
- `test.sh`
- `Directory.Packages.props`
- `docs/development/test-coverage.md`
- projetos em `tests/*`
- relatorios Cobertura, cobertura HTML ou output do pipeline fornecido pelo usuario

# Processo

1. Identifique se a tarefa pede diagnostico, priorizacao ou mudanca em testes.
2. Leia a estrategia de testes do repositorio antes de propor ferramenta nova.
3. Use o relatorio de cobertura existente quando fornecido.
4. Se for necessario gerar cobertura, prefira os comandos oficiais do projeto.
5. Classifique gaps por risco:
   - alto: codigo complexo, regra de negocio, persistencia, mensageria, seguranca, transacao, idempotencia ou erro operacional;
   - medio: fluxo de aplicacao, validacao relevante, mapeamento com contrato publico ou comportamento transversal;
   - baixo: DTO simples, configuracao declarativa, glue code trivial ou codigo gerado.
6. Priorize testes que aumentem confianca comportamental, nao apenas linhas executadas.
7. Quando sugerir novos testes, indique comportamento, cenario e motivo.
8. Quando encontrar cobertura superficial, aponte o problema explicitamente.
9. Valide com teste local ou comando proporcional quando a tarefa envolver alteracao.

# Comandos recomendados

Para validacao completa com cobertura conforme o projeto:

```powershell
./test.ps1
```

No Linux/macOS:

```bash
./test.sh
```

Para teste direto da solution com runsettings:

```bash
dotnet test ./LedgerService.slnx --configuration Release --settings ./coverlet.runsettings
```

# Saida esperada

- Diagnostico objetivo da cobertura.
- Lista priorizada de hotspots ou gaps relevantes.
- Separacao entre cobertura baixa aceitavel e cobertura baixa arriscada.
- Recomendacoes de testes por comportamento.
- Validacoes executadas ou motivo para nao executar.

# Criterio de qualidade

Um bom resultado nao e apenas aumentar percentual. Um bom resultado e reduzir risco real de regressao em areas importantes do sistema.
