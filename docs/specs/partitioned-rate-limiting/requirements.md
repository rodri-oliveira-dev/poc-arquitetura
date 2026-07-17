# Rate limiting particionado por cliente e operacao

## Diagnostico

As APIs usavam uma politica `fixed` por janela fixa aplicada ao grupo inteiro de
controllers/endpoints. Essa politica criava um limiter local por instancia da
aplicacao, sem particionamento por cliente, subject, merchant ou IP. Na pratica,
um cliente podia consumir o limite da replica e afetar os demais clientes
atendidos pela mesma instancia.

O pipeline ja processa `UseForwardedHeaders()` no inicio das APIs. Portanto,
endpoints anonimos podem usar `HttpContext.Connection.RemoteIpAddress` depois da
normalizacao do middleware, sem ler `X-Forwarded-For` diretamente. Health e
readiness ja usam `DisableRateLimiting()` e devem continuar fora da limitacao
por padrao.

Claims observadas no repositorio:

- `sub`: subject autenticado.
- `client_id`: client credentials ou cliente aplicacional.
- `azp`: cliente autorizado quando presente em tokens OIDC.
- `ClaimTypes.NameIdentifier`: fallback usado em alguns pontos de API.
- `merchant_id`: claim de autorizacao por merchant, podendo conter valores
  separados por espaco.
- `scope`: scopes de leitura, escrita e administracao por API.

Superficies analisadas:

- Endpoints autenticados de leitura: `GET` de Ledger, Balance, Transfer, Payment
  e Audit.
- Endpoints autenticados de escrita: `POST` de Ledger, Transfer, Payment, Audit
  e Identity.
- Endpoints administrativos: `OutboxAdminController`.
- Endpoint anonimo: webhook Stripe em `POST /api/v1/webhooks/stripe`.
- Health/readiness: publicos e sem limitacao por padrao.
- Swagger: documentacao habilitada por ambiente/flag; nao protegida por
  policy da API porque e registrada por middleware antes de `UseRateLimiter`,
  nao por endpoint roteado com metadata de rate limiting.

## Requisitos verificaveis

- Substituir a politica global por instancia por `PartitionedRateLimiter` ou
  mecanismo equivalente do ASP.NET Core.
- Manter a solucao local a cada replica, sem Redis ou estado compartilhado.
- Criar policies distintas para leitura autenticada, escrita autenticada,
  administracao e webhook anonimo.
- Manter options tipadas e validacao no startup.
- Permitir `PermitLimit`, `WindowSeconds` e `QueueLimit` por policy.
- Manter compatibilidade das chaves antigas `ApiLimits:RateLimitPermitLimit`,
  `ApiLimits:RateLimitWindowSeconds` e `ApiLimits:RateLimitQueueLimit` como
  defaults.
- Usar, para endpoints autenticados, composicao estavel que prioriza `sub` ou
  `ClaimTypes.NameIdentifier`, adiciona `client_id` ou `azp` quando presente e
  usa `client_id` ou `azp` sozinho para tokens machine-to-machine sem subject,
  com `merchant_id` autorizado quando presente.
- Usar fallback por IP remoto normalizado quando claims de cliente/subject
  estiverem ausentes, sem criar particao vazia compartilhada.
- Usar, para endpoints anonimos, somente `RemoteIpAddress` depois de
  `UseForwardedHeaders`.
- Nao confiar diretamente em `X-Forwarded-For` sem proxy/rede confiavel.
- Nao usar payload HTTP como chave de particao.
- Retornar `429 Too Many Requests`.
- Incluir `Retry-After` quando o lease do limiter fornecer o metadado.
- Produzir metricas de baixa cardinalidade, sem subject, merchant ou IP bruto
  como label.
- Preservar logs uteis sem expor identificadores desnecessarios.
- Nao bloquear health/readiness por padrao.

## Criterios de aceitacao

- Clientes diferentes possuem limites independentes.
- Merchants diferentes no token possuem limites independentes.
- Um cliente que excede o proprio limite recebe `429` sem afetar outro cliente.
- Webhook anonimo particiona por IP remoto confiavel.
- Headers encaminhados falsificados por cliente direto nao alteram a particao.
- Configuracao invalida de limite falha no startup.
- Documentacao explicita que o limite e por replica.
- Testes comprovam isolamento entre particoes.

## Fora do escopo

- Rate limiting distribuido, Redis ou storage compartilhado.
- Alterar scopes, policies de autorizacao ou regras de merchant.
- Usar merchant do body como chave de confianca.
- Alterar CORS.
- Alterar contratos OpenAPI manualmente.
- Fazer push, merge ou release.
