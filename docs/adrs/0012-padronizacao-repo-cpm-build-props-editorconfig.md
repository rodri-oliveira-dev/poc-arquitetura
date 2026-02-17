# ADR-0012: Padronização do repositório (CPM, Build.props, EditorConfig, Gitattributes)

## Status
Aceito

## Data
2026-02-17

## Contexto
Em um repositório com múltiplos projetos .NET, diferenças de:

- versões de pacotes NuGet;
- configurações de build (nullable, analyzers, deterministic build);
- estilo/formatação e EOL

geram drift, conflitos e builds inconsistentes entre máquinas/CI.

O repo já possui arquivos de padronização na raiz: `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig` e `.gitattributes`.

## Decisão
Adotar os seguintes padrões como regra do repositório:

- **Central Package Management (CPM)** via `Directory.Packages.props`.
- Configurações globais MSBuild via `Directory.Build.props`.
- Estilo e formatação via `.editorconfig`.
- Normalização de line endings e diffs mais limpos via `.gitattributes`.

## Consequências

### Benefícios
- Atualização de dependências centralizada.
- Builds mais previsíveis e reprodutíveis.
- Menos ruído em PRs/diffs por causa de EOL/format.

### Trade-offs / custos
- Exige disciplina para não “furar” CPM (evitar versões em `.csproj`).
- Algumas IDEs podem precisar de ajustes/atualização para respeitar `.editorconfig` plenamente.

## Alternativas consideradas

1) **Cada projeto com suas versões/configs**
   - Prós: autonomia local.
   - Contras: drift e manutenção custosa.
