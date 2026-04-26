# ADR-0030: Baseline minimo de hardening do Auth.Api

## Status
Aceito

## Data
2026-04-26

## Contexto
`LedgerService.Api` e `BalanceService.Api` ja aplicavam um baseline operacional de hardening com security headers, ProblemDetails, HSTS/HTTPS por ambiente, rate limiting e exposicao controlada de Swagger/OpenAPI.

`Auth.Api` ja possuia controles especificos da POC, como emissao de JWT RS256, publicacao de JWKS, correlation id, usuario configurado por ambiente, scopes explicitos, rate limit no endpoint de login e Swagger condicionado por ambiente/configuracao. Ainda assim, o pipeline HTTP nao aplicava o mesmo conjunto minimo de hardening das APIs de negocio.

Como o Auth.Api emite tokens e publica chaves publicas para validacao offline, ele nao deve ser o ponto mais fraco da topologia, mesmo mantendo diferencas naturais em relacao as APIs de negocio.

## Decisão
Padronizar o baseline minimo de hardening tambem no `Auth.Api`:

- adicionar `SecurityHeadersMiddleware` com os mesmos headers usados nas APIs de negocio;
- registrar `ProblemDetails` e um `GlobalExceptionHandler` para erros nao tratados;
- habilitar `UseStatusCodePages` para respostas HTTP de erro sem corpo explicito;
- aplicar `UseHsts` fora de `Development`;
- aplicar `UseHttpsRedirection` fora do ambiente `Test`, preservando o fluxo de testes automatizados e execucao local;
- manter o gating de Swagger ja existente: habilitado por padrao em `Development` e fora disso somente com `Swagger:Enabled=true`;
- manter o rate limit especifico de `POST /auth/login`, pois a API de autenticacao tem superficie menor e nao possui rotas de negocio versionadas como Ledger e Balance.

O Auth.Api continua diferente das APIs de negocio nos seguintes pontos:

- nao usa JWT Bearer no proprio endpoint de login nem no JWKS publico;
- nao possui controllers versionados nem CORS padronizado para rotas de negocio;
- nao aplica limite de body `ApiLimits`, porque o contrato atual de login e JWKS e pequeno e ja protegido por Kestrel sem exposicao de upload ou payload arbitrario;
- o rate limit permanece focado no login, que e a rota sensivel do servico.

Arquivos afetados:

- `src/Auth.Api`
- `tests/Auth.IntegrationTests`
- `README.md`

## Consequências

### Benefícios
- Reduz a superficie de exposicao do Auth.Api com headers de seguranca consistentes.
- Padroniza respostas de erro operacionais com `ProblemDetails` sem alterar o contrato nominal de sucesso do login/JWKS.
- Mantem Swagger fechado fora de `Development`, salvo excecao explicita.
- Preserva o fluxo local e os testes automatizados sem redirecionamento HTTPS no ambiente `Test`.

### Trade-offs / custos
- Respostas 404/429 geradas pelo pipeline podem passar a retornar `application/problem+json`.
- O Auth.Api ainda nao substitui um IdP OIDC real; este ajuste endurece a POC, mas nao resolve gestao de usuarios, refresh token, revogacao ou MFA.
- O rate limit segue em memoria e por instancia, adequado para a POC, mas insuficiente para um ambiente distribuido real.

## Alternativas consideradas

1. **Adotar exatamente o mesmo pipeline das APIs de negocio**
   Pros: maxima uniformidade.
   Contras: incluiria CORS, versionamento, politicas JWT e limites de rotas que nao correspondem ao papel atual do Auth.Api.

2. **Manter apenas os controles existentes do Auth.Api**
   Pros: nenhuma mudanca de comportamento.
   Contras: preservaria drift de hardening justamente no servico que emite tokens.

3. **Substituir o Auth.Api por um IdP OIDC**
   Pros: caminho mais proximo de producao.
   Contras: escopo maior, ja tratado como evolucao separada na ADR-0006.
