# ADR-0012: Não aplicar migrations automaticamente no startup

## Status
Aceito

## Data
2026-02-16

## Contexto
O README explicita que o compose não aplica migrations automaticamente, para evitar comportamento implícito em infraestrutura. Migrations são aplicadas via dotnet-ef pelo host.

## Decisão
- Manter migrations explícitas (comando manual)
- Versionar dotnet-ef em `dotnet-tools.json` e exigir `dotnet tool restore`

## Consequências
- Evita “surpresas” em runtime e dá controle do momento da mudança de schema.
- Requer disciplina (ou job explícito) para não esquecer migrations em ambientes.
- Abre caminho para um job/sidecar de migração (ver melhoria futura).

## Alternativas consideradas
- Auto-migrate no startup: simples, mas perigoso em ambientes e com múltiplas réplicas.
- Pipeline/Job de migração: melhor em produção, mas foge do objetivo imediato da PoC.
