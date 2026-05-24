# ADR-0071: Borda local opcional com Nginx e HTTPS

## Status
Aceito

## Data
2026-05-24

## Contexto

As APIs da POC rodam no Docker Compose local com HTTP interno em `ASPNETCORE_URLS=http://+:8080` e portas diretas expostas no host. Esse comportamento e simples e adequado para scripts, migrations, testes de carga e desenvolvimento diario.

Ao mesmo tempo, alguns fluxos manuais se beneficiam de uma entrada HTTPS local para validar Swagger e navegacao em um formato mais proximo de uma borda, sem promover TLS para dentro dos containers de API e sem criar dependencia obrigatoria do proxy.

## Decisao

Adicionar um overlay opcional `compose.nginx.yaml` com um container Nginx local.

O Nginx:

- publica HTTPS no host em `7443`;
- serve um portal simples em `https://localhost:7443`;
- encaminha `https://ledger.localhost:7443/*` para `ledger-service:8080`;
- encaminha `https://balance.localhost:7443/*` para `balance-service:8080`;
- encaminha `https://auth.localhost:7443/*` para `auth-api:8080`;
- normaliza `/swagger` para a Swagger UI de cada API no overlay, preservando os documentos OpenAPI em `/swagger/v1/swagger.json`;
- usa certificado montado por volume em `infra/nginx/certs`, sem versionar chaves privadas ou certificados locais.

Preferimos subdominios `.localhost` em vez de paths (`/ledger`, `/balance`, `/auth`) para preservar `/swagger` na raiz de cada API e evitar configurar `PathBase` ou reescrever OpenAPI/Swagger UI neste PR.

## Consequencias

- O `compose.yaml` principal continua independente do Nginx.
- As portas HTTP diretas continuam sendo fonte de compatibilidade para scripts, testes k6 e validacoes existentes.
- Desenvolvedores podem acessar um portal HTTPS local e Swaggers via proxy quando gerarem um certificado local adequado.
- A confianca do certificado passa a ser responsabilidade do ambiente local do desenvolvedor; `mkcert` e recomendado para reduzir atrito.
- O overlay nao implementa load balance, correlation id, logs JSON nem altera autenticacao/autorizacao das APIs.

## Alternativas consideradas

1. **Publicar APIs por path no mesmo host**
   - Rejeitado nesta etapa porque exigiria cuidado com `PathBase`, assets do Swagger UI e links gerados pelo OpenAPI.

2. **Habilitar HTTPS dentro de cada API no compose**
   - Rejeitado porque mudaria o contrato interno da stack local e aumentaria a configuracao de certificado em cada processo.

3. **Tornar Nginx obrigatorio no compose principal**
   - Rejeitado para preservar a stack minima, scripts atuais e ergonomia de desenvolvimento.
