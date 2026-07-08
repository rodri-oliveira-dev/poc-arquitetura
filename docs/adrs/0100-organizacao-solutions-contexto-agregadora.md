# ADR-0100: Organizacao de solutions por contexto e solution agregadora do repositorio

## Status
Aceito

## Data
2026-07-07

## Contexto
A solution anteriormente denominada `LedgerService.slnx` concentrava projetos de
multiplos contextos. Com a evolucao da POC para Ledger, Balance, Transfer,
Identity, Audit, Shared e testes transversais, esse nome passou a divergir da
responsabilidade real da solution, da fronteira arquitetural dos contextos, da
experiencia de desenvolvimento e das automacoes do repositorio.

Scripts, hooks, workflows e documentacao tambem passaram a usar o nome de uma
solution de dominio como marcador ou entry point global do repositorio. Isso
acoplava validacoes globais ao contexto Ledger e dificultava distinguir quando
uma validacao era contextual, transversal ou agregada.

A reorganizacao precisava continuar estrutural. Ela nao deveria alterar
contratos HTTP, eventos, topologia runtime, bancos, mensageria, Compose, k6 ou
ownership organizacional.

## Decisao
Cada contexto possui sua propria solution `.slnx`:

- `LedgerService.slnx`;
- `BalanceService.slnx`;
- `TransferService.slnx`;
- `IdentityService.slnx`;
- `AuditService.slnx`;
- `PocArquitetura.Shared.slnx`.

Cada solution contextual reune os projetos de producao e os testes diretamente
relacionados ao respectivo contexto. Dependencias auxiliares legitimas podem ser
incluidas quando necessarias para fechar restore, build ou testes do contexto.

O repositorio possui uma solution agregadora, `PocArquitetura.slnx`, que reune os
contextos ativos, Shared, `tests/Architecture.Tests` e tooling necessario para a
validacao global. A agregadora e o entry point para validacoes globais, cobertura
consolidada, workflows gerais, scripts globais e revisoes transversais.

Testes transversais pertencem a validacao agregada. `tests/Architecture.Tests`
nao deve ser atribuido artificialmente a um contexto especifico.

Scripts de infraestrutura, tooling e local development nao devem depender do
nome de uma solution de dominio para identificar a raiz do repositorio. Quando
precisarem da raiz, devem usar resolucao baseada no proprio script, no diretorio
Git ou em marcadores globais do repositorio, nao em `LedgerService.slnx`.

Dockerfiles devem preferencialmente restaurar e publicar projetos diretamente
quando a solution nao for necessaria. A organizacao em solutions e uma
organizacao de desenvolvimento, build e validacao; ela nao altera
automaticamente a topologia runtime, nomes de servicos, Compose, k6, bancos,
filas, ownership ou estrategia de deployment.

## Consequencias positivas
- Fronteiras de desenvolvimento mais claras entre contextos.
- Build e teste contextualizados quando a mudanca for local a um contexto.
- Melhor experiencia em IDEs e editores.
- Possibilidade futura de CI incremental por contexto.
- Reducao de acoplamento entre automacoes globais e nomes de dominio.
- Maior clareza entre escopo contextual de desenvolvimento e escopo global do
  repositorio.

## Trade-offs
- Aumenta o numero de arquivos `.slnx` no repositorio.
- A solution agregadora precisa permanecer sincronizada quando novos projetos
  forem adicionados.
- Existe risco de divergencia entre solutions se a governanca de novos projetos
  nao for seguida.
- Contribuidores precisam saber quando usar uma solution contextual e quando
  usar a agregadora.

## Alternativas consideradas

### 1. Manter somente uma solution global
Rejeitada como unica opcao. Uma solution global continua necessaria para
validacoes agregadas, mas so ela nao expressa bem o trabalho contextual por
bounded context e dificulta builds/testes locais mais focados.

### 2. Manter a antiga solution agregadora com nome de Ledger
Rejeitada. O nome `LedgerService.slnx` para uma agregadora global confundia
responsabilidade, fronteira arquitetural e automacoes, alem de sugerir que
Ledger era o entry point natural de todos os servicos.

### 3. Usar somente comandos por `.csproj` sem solutions
Rejeitada. Comandos por projeto sao uteis para casos pontuais, mas nao oferecem
um agrupamento explicito e versionado para validacao contextual ou global. Eles
tambem espalhariam a composicao de build entre scripts, workflows e documentacao.

### 4. Separar cada contexto em repositorio proprio neste momento
Rejeitada para o contexto atual. Um split de repositorio pode fazer sentido no
futuro, mas exigiria decisao arquitetural propria baseada em autonomia de times,
lifecycle, deployment, ownership e acoplamento real. A POC atual ainda se
beneficia de um repositorio unico com validacao agregada.

## Diretrizes de uso
- Use `PocArquitetura.slnx` para validacao global, cobertura consolidada,
  workflows gerais, alteracoes transversais e `tests/Architecture.Tests`.
- Use uma solution contextual quando a mudanca estiver restrita ao respectivo
  contexto e a validacao contextual for suficiente para o ciclo local.
- Use `PocArquitetura.Shared.slnx` para alteracoes restritas aos pacotes Shared
  e seus testes.
- Ao adicionar projeto novo, atualize a solution contextual correspondente e a
  agregadora quando o projeto fizer parte do conjunto global ativo.

## Documentacao relacionada
- [Padroes do repositorio](../development/repository-standards.md)
- [Git hooks locais](../development/git-hooks.md)
- [Validacao de Pull Requests](../development/pull-request-validation.md)
