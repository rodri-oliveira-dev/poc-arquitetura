# ADR-0037: Otimizacao de Hooks e Workflows por Arquivos Impactantes

## Status
Aceito

## Data
2026-04-26

## Contexto
O repositorio ja possui hook local de `pre-push` e workflows de GitHub Actions para build, testes, cobertura, CodeQL e revisao de dependencias. Essas validacoes sao importantes para manter a POC confiavel, mas nem toda alteracao altera codigo, build, testes, seguranca ou operacao.

Mudancas puramente documentais, como Markdown e imagens em `docs/`, nao precisam executar build/testes/cobertura nem analises que dependem de build.

## Problema
O `pre-push` executava restore, build, testes e cobertura em qualquer push. O workflow `dotnet-ci` tambem rodava para alteracoes documentais. Isso aumentava o tempo de feedback e consumia recursos sem reduzir risco quando a mudanca era claramente limitada a documentacao.

## Decisao
O hook `.githooks/pre-push` passa a calcular os arquivos alterados entre o branch local e o upstream/remoto antes de executar validacoes pesadas.

A politica de arquivos impactantes inclui codigo, projetos, solution, build, testes, configuracoes, Docker, CI, hooks, scripts e cargas de teste usadas pela automacao. Exemplos:

- `*.cs`, `*.csproj`, `*.sln`, `*.slnx`;
- `*.props`, `*.targets`, `*.runsettings`, `.editorconfig`, `global.json`, `NuGet.config`, `Directory.Build.*`, `Directory.Packages.props`, `dotnet-tools.json`, `coverlet.runsettings`;
- `*.json`, `*.yml`, `*.yaml`, `*.ruleset`;
- `Dockerfile`, `compose.yaml`, `compose.*.yaml`;
- arquivos em `src/`, `tests/`, `.github/workflows/`, `.githooks/`, `scripts/`, `tools/`, `loadtests/k6/lib/` e `loadtests/k6/scenarios/`;
- `test.sh` e `test.ps1`.

A politica de arquivos ignoraveis permite pular validacoes quando todas as alteracoes sao claramente documentais, como:

- `*.md`;
- arquivos em `docs/`;
- imagens de documentacao (`*.png`, `*.jpg`, `*.jpeg`, `*.gif`, `*.svg`, `*.webp`);
- diagramas Mermaid/LikeC4 e notas textuais que nao participam do build atual.

Em caso de mistura entre documentacao e arquivo impactante, o hook executa as validacoes completas. Se nao houver upstream/remoto configurado ou se o diff nao puder ser determinado com seguranca, o hook tambem executa as validacoes completas.

Os workflows `.github/workflows/dotnet.yml`, `.github/workflows/codeql.yml` e `.github/workflows/dependency-review.yml` passam a usar `paths-ignore` em `push` e/ou `pull_request` para ignorar mudancas apenas em Markdown, `docs/` e imagens de documentacao. A execucao agendada do CodeQL permanece ativa.

## Consequencias

### Beneficios
- Reduz tempo de push e consumo de CI para alteracoes puramente documentais.
- Mantem build, testes e cobertura obrigatorios quando houver impacto em codigo, projeto, build, teste, CI, Docker, hooks ou configuracao.
- Preserva comportamento conservador em caso de duvida.
- Alinha validacao local e CI com a mesma intencao de evitar trabalho desnecessario.

### Trade-offs / custos
- A lista de arquivos impactantes precisa ser mantida quando novos tipos de automacao forem adicionados.
- Um arquivo documental pode voltar a exigir validacao se passar a participar do build ou de algum check futuro.
- `paths-ignore` nao substitui revisao humana sobre a natureza da mudanca.

### Riscos
- Classificar indevidamente um arquivo como ignoravel poderia gerar falso negativo. Para reduzir esse risco, a regra local considera formatos de configuracao comuns como impactantes e valida quando nao consegue calcular o diff.
- Workflows filtrados por path nao rodam em PRs apenas documentais; se uma validacao futura depender de docs, ela deve ter workflow proprio ou ajustar os filtros.

## Alternativas consideradas

1. **Manter validacao pesada em todo push e PR**
   - Mais simples, mas desperdicava tempo e recursos em alteracoes sem impacto em build/testes.

2. **Usar somente `paths` com lista positiva nos workflows**
   - Mais restritivo, mas aumenta o risco de esquecer um tipo de arquivo impactante. `paths-ignore` e mais adequado porque os workflows devem rodar para quase tudo.

3. **Ignorar somente `*.md`**
   - Reduz parte do custo, mas ainda acionaria validacoes para imagens e documentos dentro de `docs/`.

4. **Pular validacao local quando nao houver upstream**
   - Mais rapido para branches novos, mas menos seguro. A decisao foi executar validacoes completas nesses casos.

## Impacto no fluxo local
Pushes com apenas documentacao informam que nao ha alteracoes impactantes e pulam build, testes e cobertura. Pushes com codigo, configuracao, workflows, hooks, Docker ou testes continuam executando restore, build, testes e gate de cobertura de 80%.

## Impacto no CI
`dotnet-ci`, `codeql` e `dependency-review` deixam de rodar para PRs/pushes apenas documentais. Mudancas impactantes continuam acionando os workflows. CodeQL tambem permanece rodando semanalmente pelo agendamento.

## Proximos passos
- Reavaliar os filtros quando houver workflow especifico para documentacao ou validacao de arquitetura.
- Atualizar a politica se arquivos LikeC4/Mermaid passarem a ser validados no pipeline.
