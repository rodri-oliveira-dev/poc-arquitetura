# Requirements - C4/LikeC4 Semantic Completion

## Objetivo

Concluir a revisao semantica dos modelos C4/LikeC4, mantendo o modelo simples e alinhado ao estado atual do codigo, Compose e infraestrutura local.

## Fonte De Verdade

A fonte de verdade para estado atual foi:

- `src/**`
- `tests/**`
- `compose.yaml`
- `compose.*.yaml`
- `infra/**`
- `contracts/**`
- `.github/workflows/**`

ADRs, specs e roadmap foram tratados como contexto historico, nao como substituto do codigo atual.

## Requisitos

- Remover `#future` de componentes implementados do `AuditService.Worker`, especialmente `AuditRecordRequestedProcessor` e `KafkaAuditRecordDeadLetterPublisher`.
- Preservar `#optional` apenas quando a ativacao depender de configuracao, profile ou overlay.
- Separar deployment local core de overlays opcionais e modo alternativo legado.
- Evitar que `Application`, `Domain` e `Infrastructure` parecam componentes C4 de runtime pertencentes simultaneamente a API e Worker.
- Diferenciar runtime, dependencia de build, implementacao de porta, registro DI e comunicacao externa.
- Atualizar jornada de leitura para `systemLandscape` -> container view -> component view -> dynamic view -> `localCoreDeployment` -> overlay quando necessario.
- Atualizar documentacao para iniciantes explicando que componente C4 nao e sinonimo de projeto, assembly, namespace, pasta ou classe.
- Gerar JSON/PNG e inspecionar visualmente as views principais.
- Executar validacoes LikeC4, build do site, testes arquiteturais e checks de diff.

## Fora De Escopo

- Alterar codigo de producao para combinar com os diagramas.
- Alterar contratos HTTP ou eventos.
- Substituir LikeC4 por Mermaid.
- Criar metamodelagem complexa ou automacao fragil baseada em nomes de arquivos.
- Publicar branch, fazer push, merge ou release.
