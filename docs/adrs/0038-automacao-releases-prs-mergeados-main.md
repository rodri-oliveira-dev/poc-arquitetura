# ADR-0038: Automacao de Releases a partir de Pull Requests Mergeados na Main

## Status
Aceito

## Data
2026-04-26

## Contexto
O repositorio possui workflows de CI para build, testes, cobertura, CodeQL e revisao de dependencias, mas nao possuia uma automacao de release. Sem uma politica explicita, tags e releases dependem de acao manual, o que reduz rastreabilidade entre PRs, commits e artefatos publicados.

O fluxo desejado e criar uma GitHub Release automaticamente quando um PR for mergeado na branch `main`, evitando releases para PRs fechados sem merge e para pushes diretos.

## Problema
E necessario padronizar quando uma release e criada, como a versao e calculada e como evitar duplicidade em reexecucoes do workflow.

Tambem e necessario manter o workflow simples, sem token externo, sem repetir build/testes ja cobertos pelo CI e sem alterar codigo de producao.

## Decisao
Criar o workflow `.github/workflows/release.yml` com gatilho:

```yaml
pull_request:
  types: [closed]
  branches: ["main"]
```

O job executa somente quando `github.event.pull_request.merged == true`.

O workflow faz checkout da `main` com historico completo, busca tags remotas, calcula a proxima tag, cria uma tag anotada no commit de merge do PR e cria uma GitHub Release com titulo igual a tag.

A release inclui metadados do PR mergeado, branch de origem, autor, commit de merge, link do PR, descricao do PR como changelog simples e uma lista resumida de commits quando disponivel.

O workflow usa somente `GITHUB_TOKEN` com:

```yaml
permissions:
  contents: write
  pull-requests: read
```

## Estrategia de versionamento
Como o repositorio nao possui um padrao SemVer ou changelog formal para releases, a decisao e usar tags sequenciais por data no formato:

```text
vYYYY.MM.DD.N
```

Exemplos:

- `v2026.04.26.1`;
- `v2026.04.26.2`.

O sufixo `N` e incrementado conforme as tags existentes no mesmo dia. A data e calculada no runner em UTC.

## Politica contra releases duplicadas
Antes de criar uma tag nova, o workflow procura uma tag no formato `vYYYY.MM.DD.N` apontando para o mesmo commit de merge.

Se encontrar tag existente:

- nao cria outra tag;
- se a release ja existir, nao cria outra release;
- se a tag existir sem release, cria a release usando a tag existente.

A concorrencia do workflow usa o grupo `release-main` com `cancel-in-progress: false`, reduzindo risco de disputa entre PRs mergeados em sequencia.

## Consequencias

### Beneficios
- Releases passam a ser rastreaveis diretamente para PRs mergeados na `main`.
- PR fechado sem merge nao gera release.
- Push direto na `main` nao gera release por este workflow.
- A estrategia `vYYYY.MM.DD.N` e simples, ordenavel e adequada enquanto nao houver SemVer formal.
- Reexecucoes do workflow evitam criar release duplicada para o mesmo commit de merge.
- O workflow nao depende de tokens externos.

### Trade-offs / custos
- A versao nao expressa compatibilidade semantica, apenas ordem cronologica.
- A data usa UTC do runner, que pode divergir do fuso local em merges proximos da meia-noite.
- O changelog depende da qualidade da descricao do PR e das mensagens de commit.
- Branch protection deve exigir os workflows de validacao antes do merge, pois o workflow de release nao repete build/testes.

### Riscos
- Se a descricao do PR ou mensagens de commit contiverem informacao sensivel, ela pode aparecer na release. A politica do repositorio continua sendo nao registrar secrets em PRs, commits ou documentacao.
- Falha entre criacao de tag e criacao de release pode deixar tag sem release. Reexecutar o workflow corrige esse caso criando a release a partir da tag existente.
- Merges simultaneos podem disputar a proxima tag diaria. O grupo de concorrencia do workflow reduz esse risco serializando a automacao de release.

## Impacto no fluxo de desenvolvimento
O fluxo normal passa a ser:

1. Abrir PR para `main`.
2. Aguardar validacoes obrigatorias do CI.
3. Fazer merge do PR.
4. O workflow de release cria tag e GitHub Release automaticamente.

Para evitar release, o PR deve ser fechado sem merge. Excecoes para entrada na `main` sem release devem ser tratadas operacionalmente antes do merge.

## Alternativas consideradas

1. **Criar release em todo push na `main`**
   - Mais simples, mas tambem criaria release para push direto ou automacoes que nao representam PR mergeado.

2. **Usar SemVer automatico baseado em Conventional Commits**
   - Mais expressivo, mas o repositorio ainda nao possui changelog formal nem convencao automatizada de bump por tipo de commit.

3. **Criar releases manualmente**
   - Mantem controle humano total, mas reduz rastreabilidade e aumenta risco de esquecimento.

4. **Executar build/testes novamente no workflow de release**
   - Aumenta defesa em profundidade, mas duplica custo. A decisao e exigir CI via branch protection antes do merge.

## Proximos passos
- Configurar branch protection da `main` para exigir `dotnet-ci` e demais checks obrigatorios antes do merge.
- Reavaliar SemVer e changelog formal se a POC evoluir para produto ou baseline compartilhado.
- Revisar periodicamente as permissoes do workflow conforme politicas do repositorio.
