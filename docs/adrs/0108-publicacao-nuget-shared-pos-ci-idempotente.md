# ADR-0108: Publicacao NuGet Shared pos-CI e idempotente

## Status
Aceito

## Data
2026-07-14

## Contexto
O workflow `publish-shared-nuget` publicava os tres pacotes Shared diretamente em `push` para `main` e em execucao manual. A mesma execucao empacotava, validava e publicava os pacotes sequencialmente.

Esse desenho tinha fragilidades operacionais:

- uma nova execucao podia cancelar uma publicacao em andamento;
- os pacotes eram enviados em sequencia, entao uma interrupcao podia deixar apenas parte do conjunto publicado;
- reruns podiam falhar apenas porque uma versao ja existia no NuGet.org;
- execucao manual publicava sem um input separado de intencao;
- havia condicoes de `pull_request` em um workflow sem gatilho de PR;
- a publicacao podia iniciar sem depender explicitamente do resultado do CI da `main`.

## Decisao
O workflow `.github/workflows/publish-shared-nuget.yml` passa a ser orientado por:

- `workflow_run` apos conclusao do `main-dotnet-ci` na `main`;
- `workflow_dispatch` com input booleano `publish`, cujo default e `false`;
- `concurrency.group: publish-shared-nuget`;
- `concurrency.cancel-in-progress: false`;
- job separado para publicacao com `permissions.id-token: write`;
- `dotnet nuget push --skip-duplicate` em todos os pacotes.

Na execucao automatica, o workflow so empacota e publica quando:

- `main-dotnet-ci` concluiu com `success`;
- o evento validado foi `push`;
- a branch validada foi `main`;
- o SHA aprovado alterou entradas relevantes dos pacotes Shared;
- o checkout usa `github.event.workflow_run.head_sha`.

Na execucao manual:

- `publish=false` restaura, compila, testa, empacota, valida e publica artifact, mas nao faz login NuGet nem push;
- `publish=true` executa as mesmas validacoes e publica somente depois de confirmar o conjunto completo de pacotes.

Antes do primeiro push, o workflow compila, testa, empacota os tres pacotes, valida os metadados dos `.nupkg`, envia o artifact e o job de publicacao confirma novamente que os tres arquivos esperados existem.

## Consequencias

### Beneficios
- Uma execucao iniciada nao e cancelada por novo push.
- Reruns da mesma versao nao falham apenas por duplicidade no NuGet.org.
- A publicacao manual exige intencao explicita.
- A publicacao automatica fica amarrada ao SHA validado pelo CI principal.
- A permissao OIDC fica isolada no job que realmente publica.

### Trade-offs / custos
- O gatilho `workflow_run` nao suporta filtro `paths`; por isso o workflow precisa detectar arquivos relevantes em uma etapa propria.
- Como o grupo de concorrencia e global para `publish-shared-nuget`, publicacoes Shared sao serializadas. Isso e conservador e evita duas publicacoes concorrentes da mesma linha de versao.
- `--skip-duplicate` torna reruns idempotentes, mas tambem exige observar o summary/logs para distinguir "ja publicado" de "publicado nesta execucao".

### Riscos
- Se o nome do workflow `main-dotnet-ci` mudar, o filtro `workflow_run` do publish deve ser atualizado junto.
- Se a lista de entradas relevantes dos pacotes Shared mudar, o detector do workflow e a documentacao de release devem ser atualizados.
