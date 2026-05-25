# ADR-0043: Package pruning e auditoria transitiva do NuGet

## Status
Aceito

## Data
2026-05-25

## Contexto

A POC usa .NET 10 nos projetos da solution e fixa o SDK em `10.0.100` pelo `global.json`. As versoes de pacotes sao centralizadas em `Directory.Packages.props` com Central Package Management.

O .NET 10 habilita auditoria transitiva do NuGet com `NuGetAuditMode=all`. Essa configuracao amplia a avaliacao de vulnerabilidades para pacotes diretos e transitivos durante o restore.

O NuGet tambem oferece package pruning com `RestoreEnablePackagePruning=true`. Essa configuracao reduz o grafo de dependencias removendo pacotes transitivos que ja sao fornecidos pela plataforma alvo.

## Decisao

Definir explicitamente, em `Directory.Build.props`, as propriedades:

- `NuGetAuditMode=all`
- `RestoreEnablePackagePruning=true`

O workflow de CI tambem executa o restore com essas propriedades explicitas para deixar o comportamento visivel no pipeline.

A verificacao de vulnerabilidades do CI continua usando `dotnet list package --vulnerable --include-transitive --no-restore --format json` e bloqueando vulnerabilidades com severidade `moderate`, `high` e `critical`.

## Consequencias

- O restore local e o restore do CI passam a declarar explicitamente auditoria transitiva e package pruning.
- Alertas transitivos podem deixar de aparecer quando o pacote vulneravel for removido do grafo pelo pruning. Isso nao significa ignorar vulnerabilidades, mas refletir que o pacote prunado nao faz parte do grafo restaurado.
- Referencias diretas redundantes podem aparecer como `NU1510`. Cada caso deve ser analisado antes de remover o `PackageReference`, considerando uso real, clareza do projeto e impacto em `Directory.Packages.props`.
- Caso `packages.lock.json` seja adotado futuramente, o package pruning pode gerar diffs esperados no lock file.
- A estrategia de bloqueio de vulnerabilidades do CI permanece baseada nas severidades `moderate`, `high` e `critical`.

## Alternativas consideradas

1. **Depender apenas dos defaults do .NET 10**
   - Rejeitado porque a configuracao explicita documenta a intencao do repositorio e reduz ambiguidade entre ambiente local e CI.

2. **Aplicar package pruning apenas no CI**
   - Rejeitado porque o grafo restaurado localmente deve ser coerente com o grafo validado no pipeline.
