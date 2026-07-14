# Ferramentas auxiliares

Este guia documenta apenas as ferramentas auxiliares usadas para gerar e validar contratos OpenAPI e documentacao arquitetural. Pre-requisitos gerais como .NET SDK, Docker-compatible API e Docker Compose ficam no [README principal](../../README.md) e em [desenvolvimento local](local-development.md).

## Camadas de ferramentas

As ferramentas usadas pelo projeto entram por tres caminhos diferentes:

| Camada | Como entra no ambiente | Exemplos neste repositorio |
| --- | --- | --- |
| Maquina do desenvolvedor | Instalacao fora do repositorio, feita pelo sistema operacional ou gerenciador local. | Node.js LTS, `npm`, `npx`. |
| Projeto via .NET local tools | Restaurada pelo manifesto `.config/dotnet-tools.json` com `dotnet tool restore`. | Swashbuckle CLI, executado como `dotnet tool run swagger`. |
| Projeto via npm | Restaurada a partir de `package.json` e `package-lock.json` com `npm ci`. | Redocly CLI e LikeC4. |

Use `npm ci` para validacoes reproduziveis. O projeto versiona as ferramentas Node em `package.json` e `package-lock.json`; por isso, evite `npx pacote@latest` em validacoes importantes, pois esse formato pode executar uma versao diferente da revisada no repositorio.

## Node.js, npm e npx

No Windows, instale Node.js LTS com:

```powershell
winget install OpenJS.NodeJS.LTS
```

Depois abra um novo terminal e valide:

```powershell
node -v
npm -v
npx -v
```

`npm` e `npx` acompanham a instalacao do Node.js. O `npm` instala as dependencias do projeto e executa scripts. O `npx` pode chamar binarios de pacotes, mas para comandos versionados do projeto prefira os scripts `npm run ...`.

## Dependencias Node do projeto

Restaure as dependencias Node com:

```bash
npm ci
```

Esse comando usa o `package-lock.json` e instala as versoes pinadas em `node_modules/`. As ferramentas relevantes sao:

| Ferramenta | Uso local | Comando recomendado |
| --- | --- | --- |
| Redocly CLI | Lint dos contratos OpenAPI versionados em `docs/openapi/`. | `npm run openapi:lint` |
| LikeC4 | Build da documentacao arquitetural em `docs/architecture/`. | `npm run architecture:build` |

## Organizacao dos scripts

Use os caminhos em subpastas de `scripts/` como padrao. A organizacao e a politica de wrappers antigos ficam em [scripts.md](scripts.md).

## Tools .NET do projeto

Restaure as tools .NET locais com:

```bash
dotnet tool restore
```

O manifesto `.config/dotnet-tools.json` define o Swashbuckle CLI como `swashbuckle.aspnetcore.cli`. O comando exposto e `swagger`, chamado pelos scripts de geracao OpenAPI via:

```bash
dotnet tool run swagger
```

Nao e necessario instalar o Swashbuckle CLI globalmente para este fluxo.

## Geracao OpenAPI

Os contratos OpenAPI sao gerados a partir dos assemblies compilados das APIs. Antes de gerar, faca build da solution:

```bash
dotnet build ./PocArquitetura.slnx --configuration Release
```

No Linux/macOS ou em shell Bash no Windows:

```bash
./scripts/contracts/openapi/generate.sh
```

No PowerShell:

```powershell
./scripts/contracts/openapi/generate.ps1
```

Os scripts geram:

- `docs/openapi/ledger.v1.json`
- `docs/openapi/balance.v1.json`
- `docs/openapi/transfer.v1.json`
- `docs/openapi/identity.v1.json`

Eles usam `dotnet tool run swagger`, configuram defaults locais para a geracao e falham se os assemblies esperados nao existirem.

## Lint OpenAPI

Depois de restaurar dependencias Node com `npm ci`, valide os contratos com:

```bash
npm run openapi:lint
```

Esse script executa o Redocly CLI versionado pelo projeto.

## Build LikeC4

Para validar a documentacao arquitetural localmente:

```bash
npm run architecture:build
```

Esse script executa o LikeC4 versionado pelo projeto e gera a saida estatica em `dist/architecture`. O diretorio `dist/` e ignorado pelo Git.

## Qualidade estatica de scripts e workflows

O workflow `script-quality` valida scripts em `scripts/` sem executar fluxos operacionais. Ele tambem valida workflows e composite actions com `actionlint`, mas separa os jobs por impacto:

- mudancas em `scripts/**`, `package.json` ou `package-lock.json` executam ShellCheck, sintaxe Bash, PSScriptAnalyzer, sintaxe PowerShell, sintaxe Node e testes Python de `scripts/quality`;
- mudancas em `.github/workflows/**` ou `.github/actions/**` executam `actionlint` para workflows e um workflow sintetico que referencia todas as composite actions locais;
- mudancas apenas em workflows/actions nao executam a suite completa de scripts.

Ele nao cobre `.agents/` nem `.githooks/`.

Para validar Bash localmente, instale o ShellCheck pelo gerenciador do seu sistema e rode:

```bash
find scripts -type f -name '*.sh' -print0 | xargs -0 shellcheck --format=gcc
find scripts -type f -name '*.sh' -print0 | xargs -0 -I {} bash -n "{}"
```

Para validar PowerShell localmente:

```powershell
Install-Module -Name PSScriptAnalyzer -RequiredVersion 1.24.0 -Scope CurrentUser
Invoke-ScriptAnalyzer -Path scripts -Recurse -Severity Error,Warning
$parseErrors = @()
Get-ChildItem -Path scripts -Recurse -File -Filter '*.ps1' | ForEach-Object {
  $tokens = $null
  $errors = $null
  [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref] $tokens, [ref] $errors) | Out-Null
  $errors | ForEach-Object {
    $parseErrors += "{0}:{1}:{2}: {3}" -f $_.Extent.File, $_.Extent.StartLineNumber, $_.Extent.StartColumnNumber, $_.Message
  }
}
if ($parseErrors) { $parseErrors; throw 'PowerShell parse failed.' }
```

Para validar scripts Node em formato `.mjs`:

```bash
find scripts -type f -name '*.mjs' -print0 | xargs -0 -I {} node --check "{}"
```

Para validar workflows localmente, baixe o `actionlint` do release oficial `rhysd/actionlint`, verifique o SHA256 do asset e execute:

```bash
actionlint -color=false .github/workflows/*.yml
```

Para composite actions, use um workflow temporario que referencie cada caminho `./.github/actions/<nome>`; esse e o mesmo modelo usado pelo CI para forcar o `actionlint` a ler os `action.yml` locais.

## Fluxo local recomendado

Para validar ferramentas auxiliares, contratos OpenAPI e documentacao arquitetural:

```bash
dotnet tool restore
npm ci
dotnet build ./PocArquitetura.slnx --configuration Release
./scripts/contracts/openapi/generate.sh
npm run openapi:lint
npm run architecture:build
```

No PowerShell, troque a geracao por:

```powershell
./scripts/contracts/openapi/generate.ps1
```
