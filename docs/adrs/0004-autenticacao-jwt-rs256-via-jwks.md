# ADR-0004: Autenticação JWT (RS256) com validação offline via JWKS

## Status
Aceito

## Data
2026-02-17

## Contexto
Os microserviços de negócio (`LedgerService.Api` e `BalanceService.Api`) expõem endpoints protegidos por autenticação e autorização por scope.

Precisamos de uma solução que:

- seja simples para a PoC;
- não crie acoplamento runtime forte (evitar chamada ao auth a cada request);
- permita validar tokens localmente e em containers;
- facilite observabilidade (correlação) e documentação no Swagger.

O repositório já possui um serviço `Auth.Api` responsável por emitir tokens e expor uma chave pública (JWKS).

## Decisão
Adotar **JWT Bearer** assinado com **RS256**, com validação offline nas APIs de negócio usando um **JWKS** obtido do `Auth.Api`:

- `Auth.Api` emite JWT via `POST /auth/login`.
- `Auth.Api` expõe JWKS público via `GET /.well-known/jwks.json`.
- `LedgerService.Api` e `BalanceService.Api` configuram `Jwt__JwksUrl` apontando diretamente para o JWKS.
- O JWKS é buscado e cacheado por `ConfigurationManager<OpenIdConnectConfiguration>` (refresh automático), evitando introspecção por request.
- Autorização é feita por **policies** baseadas na claim `scope` (string com scopes separados por espaço).

## Consequências

### Benefícios
- **Baixa latência**: sem chamada ao Auth.Api por request.
- Menor acoplamento e melhor resiliência (se o Auth estiver temporariamente indisponível, tokens já emitidos continuam validáveis até expirar e enquanto o JWKS estiver em cache).
- Modelo amplamente suportado (JWT/JWKS) e compatível com futuros providers OIDC.

### Trade-offs / custos
- Revogação de token não é imediata (depende do tempo de vida do token).
- Rotação de chaves precisa ser planejada (cache/refresh do JWKS; múltiplas chaves durante transição).
- Tokens devem ser bem configurados (issuer, audiences, scopes) para evitar permissões excessivas.

## Alternativas consideradas

1) **Introspecção por request** (token opaco)
   - Prós: revogação imediata.
   - Contras: acoplamento forte e impacto de latência/disponibilidade.

2) **Compartilhar segredo simétrico (HS256)** entre serviços
   - Prós: simples.
   - Contras: pior segurança operacional (distribuição de segredo), rotação mais dolorosa.

3) **mTLS serviço-a-serviço** + autenticação interna
   - Prós: forte para comunicação interna.
   - Contras: não cobre auth do usuário final; complexidade extra.
