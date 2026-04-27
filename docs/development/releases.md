# Releases

As releases do repositorio sao criadas automaticamente pelo workflow `.github/workflows/release.yml`.

## Quando a release e criada

O workflow escuta o evento `pull_request` com tipo `closed` para a branch `main`, mas o job so executa quando `github.event.pull_request.merged == true`.

Com isso:

- PR mergeado na `main` cria release;
- PR fechado sem merge nao cria release;
- push direto na `main` nao cria release;
- reexecucao do workflow nao cria uma segunda release para o mesmo commit de merge.

O workflow nao executa build/testes novamente. A protecao da branch `main` deve exigir o check `Build and test`, do workflow `pull-request-validation`, antes do merge.

## Formato da tag

As tags seguem o formato sequencial diario:

```text
vYYYY.MM.DD.N
```

Exemplos:

```text
v2026.04.26.1
v2026.04.26.2
```

O workflow busca as tags remotas antes de calcular a proxima versao. Se ja existir tag para o dia, incrementa o sufixo `N`. Se nao existir, usa `.1`.

Antes de criar uma nova tag, o workflow procura uma tag no formato esperado apontando para o commit de merge do PR. Se encontrar:

- reutiliza a tag existente;
- cria a release somente se a tag ainda nao possuir release;
- nao cria release duplicada quando o workflow e reexecutado.

## Conteudo da release

A release usa a tag como titulo e inclui:

- numero, titulo e link do PR;
- autor do PR;
- branch de origem;
- commit de merge;
- descricao do PR como changelog simples;
- lista resumida dos commits do PR quando disponivel.

O corpo da release usa apenas metadados do PR e commits do repositorio. Secrets nao devem ser colocados em descricoes de PR, mensagens de commit ou titulos.

## Como evitar release

Para evitar release automatica, nao faca merge do PR na `main`. Fechar o PR sem merge nao dispara release.

Se uma alteracao precisa entrar na `main` sem release, isso deve ser tratado como excecao operacional e discutido antes do merge, porque push direto na `main` tambem deve ser evitado pela protecao da branch.

## Como corrigir uma release incorreta

Se uma release for criada incorretamente:

1. Edite ou exclua a release no GitHub.
2. Avalie se a tag deve ser mantida por rastreabilidade ou removida.
3. Se remover a tag, remova tanto a tag remota quanto qualquer tag local usada na correcao.
4. Registre a correcao no PR, issue ou canal operacional correspondente.

Evite reutilizar uma tag para outro conteudo. Prefira criar uma nova release com a proxima tag sequencial quando houver duvida.

## Permissoes

O workflow usa o `GITHUB_TOKEN` padrao com permissoes minimas para esta automacao:

```yaml
permissions:
  contents: write
  pull-requests: read
```

`contents: write` permite criar tags e releases. `pull-requests: read` permite ler os metadados do PR mergeado.
