# ADR-0013: Padronização do repositório e qualidade (gitattributes, Directory.* e editorconfig)

## Status
Aceito

## Data
2026-02-16

## Contexto
O README define:
- normalização de EOL com .gitattributes
- Central Package Management com Directory.Packages.props
- defaults de build com Directory.Build.props
- regras de estilo com .editorconfig
- suporte a VS Code (workspace, extensões, tasks/launch, rest client env)

## Decisão
Manter esses arquivos como “contrato” de engenharia do repo para reduzir drift entre máquinas e CI.

## Consequências
- Menos ruído em PRs, builds mais previsíveis e dependências centralizadas.
- Aumenta consistência de time e reduz decisões repetidas por projeto.
- Exige manutenção contínua (atualização de pacotes, regras e ferramentas).

## Alternativas consideradas
- Configurar por projeto: tende a drift e divergência.
- Delegar tudo à IDE: perde enforce no CI e em ambientes distintos.
