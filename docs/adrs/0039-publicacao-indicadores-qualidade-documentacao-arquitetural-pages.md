# ADR-0039: Publicacao de Indicadores de Qualidade e Documentacao Arquitetural no GitHub Pages

## Status
Aceito

## Data
2026-04-27

## Contexto
O repositorio ja possui CI para restore, verificacao de vulnerabilidades NuGet, build, testes, cobertura e artefatos de relatorio. Tambem ja possui documentacao arquitetural LikeC4 em `docs/architecture/*.c4`, mas essa documentacao ainda nao era publicada como site navegavel.

O README tambem nao exibia indicadores visuais de build, testes, cobertura e publicacao da documentacao, reduzindo a visibilidade imediata da saude do projeto.

## Problema
E necessario expor a saude do projeto diretamente no README e publicar a documentacao LikeC4 no navegador sem alterar codigo de producao, sem duplicar regras de cobertura e sem tornar o pipeline fragil.

## Decisao
Adicionar badges no README para:

- build;
- testes;
- gate de cobertura `>= 80%`;
- publicacao da documentacao arquitetural.

Criar o workflow `.github/workflows/pages-architecture.yml` para gerar o site LikeC4 a partir de `docs/architecture` e publicar no GitHub Pages via GitHub Actions.

Documentar a operacao em `docs/development/github-pages.md` e manter a politica de cobertura em `docs/development/test-coverage.md`.

## Estrategia de badges no README
Os badges de build e testes apontam para o workflow `dotnet-ci`, pois build, testes e cobertura ja sao executados no mesmo fluxo.

O badge de cobertura representa o gate minimo da solution inteira (`>= 80%` de linhas), em vez de tentar publicar um percentual dinamico sem armazenamento estavel. O percentual real continua disponivel no artefato `test-results-and-coverage` do workflow `dotnet-ci`.

O badge de documentacao aponta para o workflow `pages-architecture`.

## Estrategia de cobertura
A cobertura permanece centralizada no fluxo existente:

- coleta por `dotnet test --collect:"XPlat Code Coverage"`;
- configuracao por `coverlet.runsettings`;
- consolidacao por ReportGenerator;
- gate minimo de 80% de cobertura total de linhas;
- publicacao de relatorio como artefato do workflow `dotnet-ci`.

Nao foi publicado relatorio de cobertura no GitHub Pages nesta decisao para evitar que o deploy de documentacao arquitetural dependa de testes .NET ou que workflows diferentes sobrescrevam o mesmo site de Pages.

## Estrategia de publicacao LikeC4
O workflow `pages-architecture` usa:

- `actions/configure-pages`;
- `likec4/actions` com `action: build`, `path: docs/architecture` e `output: dist`;
- `actions/upload-pages-artifact`;
- `actions/deploy-pages`.

O deploy ocorre apenas em `push` para `main` ou execucao manual em `main`. Em pull requests, o workflow apenas valida o build LikeC4.

## Consequencias

### Beneficios
- O README passa a mostrar rapidamente o estado de build, testes, cobertura e documentacao.
- A documentacao LikeC4 fica navegavel no GitHub Pages.
- O fluxo de Pages usa permissoes minimas para publicacao.
- A cobertura continua com a mesma regra de 80%, sem duplicar logica.
- Mudancas em `docs/architecture/**` validam a geracao do site antes do merge.

### Trade-offs / custos
- O badge de cobertura mostra o gate minimo, nao o percentual dinamico da ultima execucao.
- A visualizacao local LikeC4 exige Node.js 20+ e `npx`.
- O repositorio passa a depender da disponibilidade da action oficial do LikeC4 no GitHub Actions.

### Riscos
- Se GitHub Pages nao estiver configurado para usar GitHub Actions como fonte, o deploy pode executar sem publicar o site corretamente.
- Se a action LikeC4 alterar comportamento em uma nova versao maior, o build de documentacao pode falhar.
- Como o relatorio de cobertura fica como artefato, links historicos dependem da retencao configurada no workflow.

## Impacto no fluxo de desenvolvimento
Alteracoes em `docs/architecture/**` devem passar pelo workflow `pages-architecture` no pull request.

Alteracoes de codigo continuam passando pelo `dotnet-ci`, que executa build, testes, cobertura e gate de 80%. O relatorio detalhado fica no artefato `test-results-and-coverage`.

Para gerar a documentacao localmente, o desenvolvedor pode usar:

```bash
npx --yes likec4@latest build docs/architecture -o dist/architecture --base ./
```

## Alternativas consideradas

1. **Publicar cobertura e LikeC4 no mesmo site de Pages**
   - Daria uma rota `/coverage`, mas exigiria acoplar testes .NET ao deploy de documentacao ou coordenar workflows diferentes escrevendo no mesmo Pages artifact.

2. **Commitar saida estatica gerada pelo LikeC4**
   - Evitaria build no Pages, mas adicionaria artefatos gerados ao repositorio e aumentaria ruido em diffs.

3. **Usar servico externo para badge dinamico de cobertura**
   - Poderia mostrar o percentual da ultima execucao, mas adicionaria dependencia externa desnecessaria para a POC.

4. **Executar Pages em todo push de codigo**
   - Manteria o site sempre republicado, mas consumiria CI sem necessidade quando a documentacao arquitetural nao muda.

## Proximos passos
- Habilitar GitHub Pages com fonte "GitHub Actions" nas configuracoes do repositorio, se ainda nao estiver habilitado.
- Avaliar publicacao de relatorio de cobertura em `/coverage` se houver necessidade real de consulta publica persistente.
- Fixar uma versao especifica do LikeC4 se o uso de `latest` deixar de ser adequado para a estabilidade do pipeline.
