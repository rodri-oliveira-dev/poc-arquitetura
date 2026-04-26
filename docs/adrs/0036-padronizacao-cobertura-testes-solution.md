# ADR-0036: Padronizacao de Cobertura de Testes na Solution

## Status
Aceito

## Data
2026-04-26

## Contexto
O repositorio ja executa testes automatizados com cobertura usando `coverlet.runsettings`, `XPlat Code Coverage`, ReportGenerator, scripts locais, hook de `pre-push` e workflow de CI.

Antes desta decisao, os limites estavam divergentes entre os pontos de execucao: scripts locais usavam 85%, o CI usava 50% e o hook local usava 80%. Tambem nao havia configuracao explicita para ignorar itens anotados com `ExcludeFromCodeCoverage`.

## Problema
A divergencia de threshold cria resultados inconsistentes entre execucao local, hooks e CI. Alem disso, sem politica explicita de exclusao por atributo, classes, metodos ou membros que nao devem entrar no calculo podem afetar artificialmente a cobertura consolidada.

## Decisao
Padronizar a cobertura de testes da solution inteira (`LedgerService.slnx`) com as seguintes regras:

- coletar cobertura com `dotnet test ./LedgerService.slnx --collect:"XPlat Code Coverage" --settings ./coverlet.runsettings`;
- consolidar os relatorios `coverage.cobertura.xml` com ReportGenerator;
- validar cobertura total de linhas minima de 80%;
- aplicar a mesma regra nos scripts locais (`test.ps1` e `test.sh`), no hook `.githooks/pre-push` e no workflow `.github/workflows/dotnet.yml`;
- excluir do calculo itens anotados com `System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute` ou `ExcludeFromCodeCoverageAttribute`;
- preservar exclusoes legitimas para `GeneratedCodeAttribute`, `CompilerGeneratedAttribute`, `Program.cs`, migrations EF e arquivos gerados.

`ExcludeFromCodeCoverage` deve ser usado apenas quando houver justificativa tecnica clara. Codigo de producao nao deve ser excluido apenas para elevar a cobertura.

## Consequencias

### Beneficios
- Elimina drift entre validacao local, hooks e CI.
- Garante que a metrica representa a cobertura consolidada da solution inteira.
- Permite excluir pontos explicitamente marcados sem criar filtros artificiais por projeto.
- Mantem uma unica fonte de configuracao para coleta em `coverlet.runsettings`.

### Trade-offs / custos
- O gate de 80% pode exigir novos testes antes de aceitar mudancas futuras.
- A consolidacao por ReportGenerator adiciona uma etapa obrigatoria aos scripts e ao hook.
- O `pre-push` depende de Python para ler o `Summary.json`, assim como o script Bash local.

### Riscos
- Uso indevido de `ExcludeFromCodeCoverage` pode mascarar falta real de testes.
- Diferencas de ambiente local podem impedir a execucao do hook quando ferramentas basicas nao estiverem disponiveis.
- A cobertura consolidada pode esconder baixa cobertura em um projeto especifico, ainda que preserve a regra global da POC.

## Alternativas consideradas

1. **Manter thresholds diferentes por ambiente**
   - Simples no curto prazo, mas mantem drift e reduz confianca no CI como fonte de verdade.

2. **Aplicar threshold por projeto**
   - Aumenta rigor por componente, mas e mais sensivel a projetos pequenos e nao atende ao objetivo atual de validar a solution inteira.

3. **Usar somente o calculo bruto dos arquivos Cobertura**
   - Evita ReportGenerator no hook, mas duplica logica de consolidacao e pode divergir dos scripts e do CI.

4. **Subir o limite para 85%**
   - Mais rigoroso, mas nao corresponde ao objetivo definido para esta etapa.

## Impacto no fluxo local
Desenvolvedores devem usar `./test.ps1` ou `./test.sh` para executar testes da solution com cobertura. O hook de `pre-push` aplica a mesma politica antes do envio de alteracoes.

## Impacto no CI/hooks
O workflow `dotnet-ci` e o hook `.githooks/pre-push` passam a validar 80% de cobertura total de linhas com a mesma configuracao de coleta e consolidacao.

## Proximos passos
- Monitorar se o threshold global de 80% continua adequado conforme a POC crescer.
- Avaliar gates complementares por projeto apenas se a cobertura consolidada deixar de representar risco real.
- Revisar usos futuros de `ExcludeFromCodeCoverage` durante code review.
