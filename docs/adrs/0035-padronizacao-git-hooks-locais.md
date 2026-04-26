# ADR-0035: Padronizacao de Git Hooks Locais para Commit Semantico, Build, Testes e Cobertura

## Status
Aceito

## Data
2026-04-26

## Contexto
O repositorio ja adota padroes de engenharia para build, testes, cobertura, documentacao e governanca por ADRs. Ainda assim, parte dessas validacoes dependia da disciplina manual do desenvolvedor antes de criar commits ou enviar alteracoes.

O fluxo local precisa reforcar duas garantias antes do compartilhamento de codigo:

- mensagens de commit consistentes com Conventional Commits;
- build, testes e cobertura minima executados antes do push.

## Problema
Sem hooks locais versionados, cada maquina pode ter validacoes diferentes ou nenhuma validacao. Isso aumenta o risco de commits fora do padrao, pushes com build quebrado, testes falhando ou queda de cobertura descoberta tarde demais.

## Decisao
Versionar hooks locais em `.githooks/`:

- `commit-msg` valida a primeira linha da mensagem usando Conventional Commits com os tipos aceitos pelo repositorio.
- `pre-push` executa `dotnet restore`, `dotnet build --no-restore`, `dotnet test` com `coverlet.runsettings` e valida cobertura total minima de 80%.

Configurar automaticamente `git config core.hooksPath .githooks` apos o build de `src/BalanceService.Api/BalanceService.Api.csproj`.

O target MSBuild deve ser idempotente, ignorar CI (`CI=true`), tolerar ambientes sem Git e nao executar hooks durante o build.

## Alternativas consideradas

1. **Documentar os comandos sem hooks**
   - Simples, mas depende de disciplina manual e nao impede commits/pushes inconsistentes.

2. **Instalar hooks por script manual**
   - Funciona, mas cria um passo extra facil de esquecer em novas maquinas.

3. **Usar hooks globais do Git**
   - Evita arquivos no repositorio, mas reduz reprodutibilidade e pode conflitar com outros projetos.

4. **Adicionar ferramenta externa de hooks**
   - Poderia oferecer ergonomia extra, mas adicionaria dependencia desnecessaria para uma POC .NET.

## Consequencias

### Beneficios
- Padroniza mensagens de commit antes que entrem no historico local.
- Aproxima o feedback de build, testes e cobertura do momento do push.
- Reaproveita o padrao de cobertura existente com `coverlet.runsettings`.
- Reduz configuracao manual de onboarding ao atrelar a instalacao ao build do BalanceService.Api.

### Trade-offs / custos
- O `pre-push` aumenta o tempo de push, pois executa restore, build e testes.
- Desenvolvedores precisam ter o SDK .NET e ferramentas POSIX basicas disponiveis localmente para a validacao de cobertura.
- Hooks locais podem ser ignorados com `--no-verify`, portanto nao substituem CI.

### Riscos
- Diferencas de ambiente local podem gerar falhas que nao aparecem em CI.
- O hook pode tornar pushes frequentes mais lentos em maquinas com poucos recursos.
- Se o Git nao preservar bit executavel em algum ambiente, pode ser necessario ajustar permissao com `chmod +x .githooks/*`.

## Impacto no fluxo local
Ao construir `src/BalanceService.Api/BalanceService.Api.csproj`, o repositorio passa a apontar `core.hooksPath` para `.githooks`. Depois disso:

- commits fora do padrao semantico falham no `commit-msg`;
- pushes executam build, testes e gate de cobertura minima de 80%.

## Impacto no CI
O target MSBuild nao configura hooks quando `CI=true`. Os hooks sao uma guarda local e nao substituem os checks de pipeline.

## Proximos passos
- Manter o threshold local alinhado com a politica de qualidade do repositorio.
- Avaliar mover validacoes equivalentes para CI quando ainda nao existirem.
- Revisar o custo do `pre-push` se o tempo de testes crescer de forma significativa.
