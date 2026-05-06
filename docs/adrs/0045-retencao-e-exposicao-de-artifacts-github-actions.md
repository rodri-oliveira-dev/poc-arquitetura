# ADR-0045: Retencao e exposicao de artifacts no GitHub Actions

## Status
Aceito

## Data
2026-05-06

## Contexto
Os workflows do GitHub Actions publicam artifacts de testes, cobertura e mutation testing para apoiar diagnostico apos execucoes remotas.

Esses artifacts podem expor nomes de testes, paths internos do repositorio, stack traces, estrutura de assemblies e trechos de codigo renderizados em HTML ou JSON. Em repositorio publico, isso aumenta a exposicao desnecessaria sem melhorar todos os diagnosticos na mesma proporcao.

O objetivo nao e ocultar falhas, vulnerabilidades, cobertura baixa ou resultados tecnicos. O objetivo e publicar apenas o necessario para investigacao, com nomes claros e retencao curta.

## Decisao
Manter a publicacao de artifacts nos workflows existentes sem renomear workflows ou jobs:

- `.github/workflows/dotnet.yml`;
- `.github/workflows/mutation-tests.yml`.

No workflow `dotnet-ci`, manter o artifact `test-results-and-coverage` com retencao de 7 dias e publicar somente:

- arquivos `.trx`;
- arquivos `coverage.cobertura.xml`;
- `coverage-report/Summary.json`;
- `coverage-report/Summary.txt`.

O relatorio HTML completo de cobertura continua sendo gerado no runner para o gate e resumo do job, mas deixa de ser publicado como artifact porque o XML e os summaries atendem ao diagnostico principal com menor exposicao.

No workflow `Mutation Tests`, manter os artifacts separados por alvo:

- `stryker-ledger-service-application`;
- `stryker-balance-service-application`.

Cada artifact passa a publicar apenas o `mutation-report.html` gerado pelo Stryker.NET, com retencao de 7 dias. A pasta `StrykerOutput/` completa e o JSON detalhado deixam de ser publicados.

Todos os uploads continuam com `if-no-files-found: warn`, preservando o comportamento atual e evitando falha secundaria falsa quando uma etapa anterior falhar antes de gerar arquivos.

A documentacao operacional da politica fica em `docs/development/workflow-artifacts.md`, com link no README.

## Consequencias

### Beneficios
- Reduz exposicao de relatorios HTML/JSON detalhados em repositorio publico.
- Mantem artifacts uteis para diagnostico de falhas de teste, cobertura e mutation testing.
- Mantem o gate minimo de cobertura de 80%.
- Mantem mutation testing como fluxo informativo, sem transformar score em bloqueio.
- Torna explicita a retencao e o motivo de cada artifact publicado.

### Trade-offs / custos
- Quem precisar navegar cobertura por arquivo pelo HTML completo deve reproduzir localmente ou consultar os summaries/XML publicados.
- O relatorio HTML do Stryker ainda pode conter trechos de codigo, mas e mantido por ser o principal meio de analise humana dos mutantes.
- O JSON detalhado do Stryker deixa de ficar disponivel para automacoes futuras via artifact; se essa necessidade aparecer, deve ser reavaliada explicitamente.

### Riscos
- Reduzir demais os artifacts pode dificultar uma investigacao apos o fim da retencao.
- O `mutation-report.html` ainda representa risco residual de exposicao de detalhes internos.
- Alteracoes futuras em workflows podem reintroduzir uploads detalhados sem revisao se a politica nao for mantida.

## Alternativas consideradas

1. **Remover todos os artifacts**
   - Rejeitado porque dificultaria diagnostico de falhas remotas e investigacao de cobertura/mutation testing.

2. **Manter todos os relatorios completos e apenas documentar o risco**
   - Rejeitado porque nao reduziria exposicao desnecessaria.

3. **Publicar HTML completo de cobertura**
   - Rejeitado porque `Summary.json`, `Summary.txt` e `coverage.cobertura.xml` ja preservam informacao suficiente para o gate e diagnostico inicial.

4. **Publicar JSON do Stryker junto com HTML**
   - Rejeitado nesta etapa porque o JSON detalhado aumenta volume e exposicao; o HTML permanece como leitura primaria.

## Proximos passos
- Reavaliar a necessidade do HTML do Stryker quando houver summary automatizado confiavel no GitHub Step Summary.
- Reavaliar retencao se o volume de artifacts ou o risco de exposicao mudar.
- Manter testes de politica para novos usos de `actions/upload-artifact`.
