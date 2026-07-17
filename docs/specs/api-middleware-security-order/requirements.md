# Ordenacao de middlewares e seguranca do Swagger

## Contexto

As APIs Ledger, Balance, Transfer, Payment, Identity e Audit usam defaults
compartilhados em `src/Shared/ApiDefaults`. Antes desta spec, todas chamavam
`UseForwardedHeaders()`, depois Swagger e somente entao `UseApiDefaults()`.

Essa ordem preservava a Swagger UI, mas deixava Swagger UI e OpenAPI JSON fora
dos headers de seguranca e exigia um middleware dedicado apenas para
`X-Content-Type-Options: nosniff` dentro da configuracao do Swagger.

## Requisitos verificaveis

- Definir uma ordem canonica para o pipeline HTTP das APIs.
- Manter `UseForwardedHeaders` antes de componentes dependentes de scheme, host
  ou IP.
- Aplicar exception handler e status code pages de forma uniforme.
- Aplicar correlation ID em respostas normais, erros, health e redirecionamento
  HTTPS.
- Aplicar security headers aos endpoints normais, health, OpenAPI JSON e
  Swagger UI.
- Remover middleware redundante usado apenas para `X-Content-Type-Options`.
- Manter OpenAPI JSON com CSP global restrita.
- Usar CSP especifica para Swagger UI para preservar scripts e estilos do
  Swashbuckle.
- Restringir qualquer `unsafe-inline` somente a Swagger UI.
- Preservar Swagger desabilitado fora de `Development`, salvo
  `Swagger:Enabled=true`.
- Preservar health/readiness sem autenticacao.
- Preservar autenticacao e autorizacao dos endpoints de negocio.
- Preservar rate limiting existente, sem redesenho de particionamento.
- Nao alterar contratos HTTP, payloads, status codes nem policies de
  autorizacao.
- Manter compatibilidade com geracao/lint/diff OpenAPI e OWASP ZAP.

## Criterios de aceitacao

- Swagger UI carrega quando habilitada.
- OpenAPI JSON possui headers de seguranca, sem `unsafe-inline`.
- Endpoints normais mantem o comportamento atual de autenticacao, autorizacao,
  rate limiting e CORS.
- Health endpoints continuam anonimos conforme decisao atual.
- Nao ha headers de seguranca duplicados.
- Requisicoes HTTP redirecionadas para HTTPS recebem correlation ID e headers
  de seguranca nos ambientes em que o redirecionamento fica ativo.
- A ordem canonica esta documentada e testada.

## Fora do escopo

- Alterar CORS.
- Alterar policies de autorizacao.
- Implementar rate limiting particionado.
- Trocar Swashbuckle.
- Alterar contratos HTTP ou JSONs OpenAPI manualmente.
- Remover, relaxar ou contornar testes OWASP ZAP.
- Fazer push, merge ou release.
