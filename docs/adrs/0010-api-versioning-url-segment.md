# ADR-0010: Versionamento de API via URL segment e Swagger multi-versão

## Status
Aceito

## Data
2026-02-16

## Contexto
O README define Asp.Versioning e formato `api/v{version}/...`, com versão padrão v1.

## Decisão
- Usar URL segment versioning
- Publicar Swagger por versão
- Manter v1 como default

## Consequências
- Contratos claros e coexistência de versões.
- Facilidade de testar via Swagger e clientes.
- Aumenta esforço de manutenção se houver muitas versões ativas.

## Alternativas consideradas
- Header-based versioning: mais “limpo” na URL, porém menos óbvio para quem testa manualmente.
- Query string versioning: simples, mas costuma ser menos preferido em governança de API.
