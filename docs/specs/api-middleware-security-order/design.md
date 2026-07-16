# Design

## Analise do pipeline anterior

As seis APIs analisadas (`LedgerService.Api`, `BalanceService.Api`,
`TransferService.Api`, `PaymentService.Api`, `IdentityService.Api` e
`AuditService.Api`) usavam a mesma ordem:

1. `UseForwardedHeaders`
2. Swagger/OpenAPI
3. `UseApiDefaults`
4. `UseAuthentication`
5. `UseAuthorization`
6. health endpoints
7. controllers ou minimal APIs com rate limiting

Dentro de `UseApiDefaults`, a ordem anterior era:

1. `UseHsts` fora de `Development`
2. `UseExceptionHandler`
3. `UseStatusCodePages`
4. `UseHttpsRedirection` fora de `Test`
5. correlation ID
6. limite de body
7. security headers
8. CORS
9. rate limiting

Riscos encontrados:

- OpenAPI JSON e Swagger UI ficavam fora dos security headers compartilhados.
- Swagger tinha um middleware extra apenas para `X-Content-Type-Options`.
- Uma CSP global aplicada sem excecao quebraria a Swagger UI do Swashbuckle por
  uso de scripts e estilos inline.
- Redirecionamentos HTTPS podiam ocorrer antes de correlation ID e security
  headers.

## Ordem canonica

Cada API deve montar o pipeline assim:

1. `UseForwardedHeaders`
2. `UseApiDefaults`
3. Swagger/OpenAPI, via `UseApiSwagger` ou `UseApiSwaggerDefaults`
4. `UseAuthentication`
5. `UseAuthorization`
6. health/readiness anonimos
7. controllers ou minimal APIs de negocio com rate limiting atual

Dentro de `UseApiDefaults`, a ordem canonica fica:

1. `UseHsts` fora de `Development`
2. correlation ID
3. security headers
4. `UseExceptionHandler`
5. `UseStatusCodePages`
6. `UseHttpsRedirection` fora de `Test`
7. limite de body
8. CORS
9. rate limiting

## CSP

APIs, health e OpenAPI JSON usam CSP global restrita:

```text
default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'
```

Swagger UI usa CSP especifica:

```text
default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'
```

O uso de `unsafe-inline` e restrito aos caminhos de UI do Swagger sob
`/swagger`, porque a UI padrao do Swashbuckle depende de scripts e estilos
inline. OpenAPI JSON em `/swagger/{version}/swagger.json` nao usa essa politica
especial.

## Compatibilidade

- Swagger continua desabilitado fora de `Development`, salvo
  `Swagger:Enabled=true`.
- Swagger permanece antes de autenticacao e autorizacao para preservar a
  exposicao atual quando habilitado.
- Health endpoints continuam mapeados sem `RequireAuthorization`.
- Rate limiting permanece com a politica existente aplicada aos endpoints de
  negocio por `RequireRateLimiting("fixed")`.
- Como contratos HTTP nao mudam, a geracao OpenAPI nao deve produzir alteracoes
  de contrato.
