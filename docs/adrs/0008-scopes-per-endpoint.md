# ADR-0008: Autorização por scopes por endpoint

## Status
Aceito

## Data
2026-02-16

## Contexto
Há necessidade de separar permissões:
- Ledger write
- Balance read

O README define claim `scope` (string com scopes separados por espaço) e políticas por endpoint.

## Decisão
- Adotar claim `scope` (não `scp`) contendo scopes separados por espaço
- Exigir:
  - `ledger.write` no `POST /api/v1/lancamentos`
  - `balance.read` no `GET` de consolidado diário e período

## Consequências
- Controle simples e objetivo de autorização.
- Facilita extensão para novos endpoints/scopes.
- Requer padronização do emissor (Auth) e compatibilidade futura com IdP padrão (ver ADR-0100).

## Alternativas consideradas
- Roles only: funciona, mas scopes mapeiam melhor “permissões por API”.
- ACL por merchantId: mais fino, porém não descrito no README e amplia escopo.
