# ADR-0109: Setup Explicito de Git Hooks Locais

## Status
Aceito

## Data
2026-07-14

## Contexto
A ADR-0035 padronizou hooks locais versionados em `.githooks/` e instalava `core.hooksPath` automaticamente durante o build do `BalanceService.Api`.

Esse acoplamento tornou o build de um servico responsavel por modificar uma configuracao local do Git. Alem disso, um valor local existente de `core.hooksPath` podia ser sobrescrito silenciosamente, desativando hooks pessoais, corporativos ou de outras ferramentas.

## Decisao
Remover a configuracao automatica de `core.hooksPath` do build do `BalanceService.Api`.

A instalacao passa a ser um passo explicito de onboarding por scripts dedicados:

- `scripts/setup/configure-git-hooks.sh`;
- `scripts/setup/configure-git-hooks.ps1`.

Os scripts:

- identificam a raiz do repositorio Git;
- alteram somente `git config --local core.hooksPath`;
- configuram `.githooks` apenas quando nao ha valor local existente;
- sao idempotentes quando o valor ja e `.githooks`;
- recusam sobrescrever outro valor sem `--force` ou `-Force`;
- possuem modo `--check` ou `-Check` somente leitura;
- validam `.githooks/commit-msg`, `.githooks/post-merge` e `.githooks/pre-push`;
- validam permissao executavel em Unix e aplicam `chmod +x` somente em modo de instalacao;
- nao usam configuracao global, nao exigem privilegios administrativos e nao executam hooks durante a instalacao.

## Alternativas consideradas

1. **Manter o target MSBuild**
   - Reduz passo manual, mas preserva efeito colateral no build e risco de sobrescrita silenciosa.

2. **Configurar hooks globalmente**
   - Evita configuracao por clone, mas conflita com outros repositorios e reduz reprodutibilidade.

3. **Adicionar ferramenta externa de hooks**
   - Oferece conveniencia, mas adiciona dependencia operacional desnecessaria para a POC.

4. **Documentar apenas `git config core.hooksPath .githooks`**
   - Simples, mas nao valida pre-condicoes, nao protege valores existentes e nao oferece modo de verificacao.

## Consequencias

### Beneficios

- Builds deixam de modificar configuracao local do Git.
- Onboarding fica explicito, transparente e reproduzivel.
- Configuracoes locais existentes sao preservadas por padrao.
- `--check`/`-Check` permite validar instalacao sem efeitos colaterais.
- Bash e PowerShell oferecem comportamento equivalente para Linux, macOS e Windows.

### Trade-offs / custos

- Desenvolvedores precisam executar um comando adicional ao clonar o repositorio.
- Quem ja possui outro `core.hooksPath` precisa decidir conscientemente entre manter a configuracao atual, integrar os hooks manualmente ou usar `--force`/`-Force`.

## Impacto no fluxo local

Depois de clonar o repositorio, execute:

```bash
./scripts/setup/configure-git-hooks.sh
./scripts/setup/configure-git-hooks.sh --check
```

No Windows:

```powershell
./scripts/setup/configure-git-hooks.ps1
./scripts/setup/configure-git-hooks.ps1 -Check
```

Para remover a configuracao local:

```bash
git config --local --unset core.hooksPath
```

## Impacto no CI

Nenhum. Os scripts sao executados sob demanda e nao fazem parte de build, restore, testes, hooks, workflows ou inicializacao de aplicacoes.
