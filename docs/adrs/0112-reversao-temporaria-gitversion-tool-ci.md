# ADR-0112: Reversao temporaria do GitVersion.Tool 7.0.1 na CI

## Status
Aceito

## Data
2026-11-23

## Relacao com outras decisoes
Complementa a [ADR-0048](./0048-versionamento-semantico-gitversion-commits-semanticos.md). Nao altera a adocao do GitVersion como fonte oficial de versionamento semantico.

## Contexto
O [PR #175](https://github.com/rodri-oliveira-dev/poc-arquitetura/pull/175), aberto pelo Dependabot, atualizou o `GitVersion.Tool` de `6.8.2` para `7.0.1` no manifesto `.config/dotnet-tools.json`, junto de outras dependencias. Os workflows `main-dotnet-ci` e `openapi-contract-validation` falharam antes de compilar ou validar contratos, na etapa `dotnet tool restore` executada pela action composta `.github/actions/setup-dotnet/action.yml`.

Nos dois jobs, o restore informou que os comandos `dotnet-sonarscanner` (pacote `dotnet-sonarscanner`) e `swagger` (pacote `swashbuckle.aspnetcore.cli`) nao estavam presentes nos respectivos pacotes, relatando `dotnet-gitversion` como comando encontrado. As versoes dessas duas ferramentas nao foram modificadas no PR.

Os jobs restauraram um cache NuGet anterior por `restore-keys`, com cerca de 1,14 GB. Isso deixa aberta a possibilidade de interferencia do cache, alem de um problema de compatibilidade ou empacotamento envolvendo a versao atualizada do GitVersion. Os logs isoladamente nao demonstram a causa raiz.

Na [execucao anterior da CI em 16/11/2026](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/43883072578), a restauracao das seis ferramentas teve sucesso utilizando `GitVersion.Tool 6.8.2`. O [job de contratos OpenAPI anterior](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/43883072555) tambem concluiu com sucesso.

## Decisao
- Reverter exclusivamente `gitversion.tool` de `7.0.1` para `6.8.2` em `.config/dotnet-tools.json`, mantendo `rollForward: false`.
- Preservar as demais atualizacoes do PR #175, inclusive a versao atualizada do ReportGenerator.
- Manter a estrategia de SemVer, as convencoes de commit e o uso de `dotnet-gitversion` definidos na ADR-0048.
- Tratar a reversao como mitigacao temporaria e nao como prova de defeito no GitVersion 7.
- Nao desabilitar checks nem mascarar falhas do `dotnet tool restore`.

## Consequencias
- O restore passa a solicitar a versao de GitVersion conhecida por funcionar na execucao anterior.
- Se os dois workflows voltarem a passar, o bloqueio do PR fica mitigado, mas a causa raiz ainda exigira investigacao.
- A atualizacao para a linha 7.x fica adiada; novas correcoes e funcionalidades dessa linha nao entram enquanto a pinagem for mantida.

## Validacao e criterio de reversao desta decisao
1. Executar novamente `Build and test` e `Validate OpenAPI contracts` sobre o novo commit do PR #175; ambos devem concluir com sucesso.
2. Se persistir o erro, comparar uma restauracao limpa sem cache de pacotes com a restauracao que utiliza `restore-keys`, sem assumir que o GitVersion seja o causador.
3. Reintroduzir GitVersion 7.x apenas em alteracao isolada, com `dotnet tool restore` limpo, execucao integral dos checks e validacao do comando de release `dotnet tool run dotnet-gitversion /showvariable MajorMinorPatch`.
4. Registrar o resultado da investigacao e atualizar esta ADR se o diagnostico confirmar outra causa.

## Alternativas consideradas
- **Manter 7.0.1 e apenas reexecutar os jobs:** insuficiente para isolar a mudanca que antecedeu a falha.
- **Limpar globalmente o cache NuGet:** pode resolver um problema de cache, mas altera outra variavel no mesmo experimento; fica como proxima etapa se a reversao nao funcionar.
- **Reverter todas as dependencias do PR:** escopo desnecessariamente amplo e perda de atualizacoes sem relacao com a falha observada.

## Evidencias
- [Falha no Build and test](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/45175135907/job/137694297431).
- [Falha na validacao OpenAPI](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/45175135674/job/137694297041).
