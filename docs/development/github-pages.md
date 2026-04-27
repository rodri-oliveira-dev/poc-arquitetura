# GitHub Pages e documentacao LikeC4

Este repositorio publica a documentacao arquitetural LikeC4 no GitHub Pages pelo workflow `pages-architecture`.

## Fonte

Os arquivos fonte ficam em:

- `docs/architecture/model.c4`
- `docs/architecture/views.c4`
- demais arquivos de apoio em `docs/architecture/`

O site publicado e gerado a partir dos arquivos `*.c4` dessa pasta.

## Workflow

O deploy e feito por `.github/workflows/pages-architecture.yml`.

O workflow roda em:

- `push` para `main` quando houver alteracao em `docs/architecture/**` ou `.github/workflows/**`;
- `pull_request` para `main` nos mesmos caminhos, apenas para validar o build;
- `workflow_dispatch`, para execucao manual.

Permissoes usadas:

- `contents: read`, para ler o repositorio;
- `pages: write`, para publicar o artefato no GitHub Pages;
- `id-token: write`, exigido pelo deploy oficial de Pages.

## Geracao local

Requisitos:

- Node.js 20+;
- `npm`/`npx`.

Comando para gerar o site estatico:

```bash
npx --yes likec4@latest build docs/architecture -o dist/architecture --base ./
```

O diretorio `dist/` e ignorado pelo git e nao deve ser versionado.

Para visualizar durante a edicao:

```bash
npx --yes likec4@latest start docs/architecture
```

## Validacao antes do push

Antes de enviar alteracoes de arquitetura:

1. Gere o site localmente com `npx --yes likec4@latest build docs/architecture -o dist/architecture --base ./`.
2. Confirme que os arquivos `*.c4` continuam parseando sem erro.
3. Abra um pull request para que o workflow `pages-architecture` valide o build.

## GitHub Pages

O repositorio deve ter GitHub Pages configurado para usar GitHub Actions como fonte de publicacao.

URL esperada:

```text
https://rodri-oliveira-dev.github.io/poc-arquitetura/
```

## Falhas comuns

- `npx` indisponivel localmente: instale Node.js 20+ ou valide pelo workflow do pull request.
- Erro de parse LikeC4: revise `docs/architecture/model.c4` e `docs/architecture/views.c4`.
- Deploy sem URL publicada: confira se GitHub Pages esta habilitado com fonte "GitHub Actions".
- Permissao negada no deploy: confirme as permissoes `pages: write` e `id-token: write` no workflow e as regras do ambiente `github-pages`.
