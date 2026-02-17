# ADR-0007: JWT RS256 com JWKS e validação offline nos serviços

## Status
Aceito

## Data
2026-02-16

## Contexto
Ledger e Balance devem validar tokens sem chamar o Auth.Api a cada request. O README define RS256 e endpoint JWKS, com cache/refresh.

## Decisão
Adotar:
- Auth.Api emitindo JWT RS256 (`POST /auth/login`)
- Exposição de JWKS público (`GET /.well-known/jwks.json`)
- Ledger/Balance validam assinatura/issuer/audience usando JWKS com cache e refresh (ConfigurationManager)
- Sem introspecção por request

## Consequências
- Baixa latência e menos dependência do Auth.Api no caminho crítico.
- Permite rotação de chaves (com cuidado) e cache controlado.
- Exige governança de issuer/audience e compatibilidade com bibliotecas padrão.
- Implementação atual de `aud` como string separada por espaço é atípica e pode exigir customizações.

## Alternativas consideradas
- Introspection por request: mais controle, porém aumenta latência e acopla disponibilidade.
- HS256 (segredo compartilhado): simplifica, mas piora distribuição/rotação segura do segredo.
