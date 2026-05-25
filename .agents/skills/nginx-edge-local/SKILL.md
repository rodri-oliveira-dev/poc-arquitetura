---
name: nginx-edge-local
description: Use esta skill para revisar, alterar ou diagnosticar a borda local Nginx da POC, incluindo compose.nginx.yaml, infra/nginx, HTTPS local, proxy reverso, headers, limites defensivos, logs, Swagger via subdominios .localhost e load balance local do LedgerService.Api. Nao use para mudancas funcionais das APIs .NET.
---

# Objetivo

Orientar mudancas pequenas, seguras e verificaveis na borda local Nginx da POC.

A borda Nginx deste repositorio e opcional, local e voltada para desenvolvimento, demonstracao de HTTPS local, portal, Swagger via proxy, headers de seguranca, limites defensivos e load balance local do `LedgerService.Api`.

# Quando usar

- Alterar ou revisar `infra/nginx/nginx.conf`.
- Alterar ou revisar `infra/nginx/security-headers.conf`.
- Alterar ou revisar `infra/nginx/Dockerfile`.
- Alterar ou revisar `infra/nginx/html/`.
- Alterar ou revisar `compose.nginx.yaml`.
- Diagnosticar proxy reverso, upstream, load balance, TLS local, certificados locais, Swagger via Nginx ou subdominios `.localhost`.
- Revisar headers, cache, fingerprinting, limites de body, timeouts, rate limiting, connection limiting ou logs do Nginx.
- Avaliar impacto de mudancas Nginx em README, docs, LikeC4 ou ADRs.

# Quando nao usar

- Implementar regra de negocio nas APIs .NET.
- Alterar handlers, entidades, migrations, EF Core, Kafka, Outbox ou autenticacao sem relacao direta com a borda Nginx.
- Corrigir testes de aplicacao que nao exercem a borda local.
- Transformar a borda local em requisito obrigatorio da stack minima sem pedido explicito.
- Desenhar estrategia de producao, Kubernetes, ingress real, service mesh ou gateway corporativo sem pedido explicito.

# Fontes de verdade

Consulte primeiro:

1. `AGENTS.md`
2. `compose.nginx.yaml`
3. `infra/nginx/nginx.conf`
4. `infra/nginx/security-headers.conf`
5. `infra/nginx/Dockerfile`
6. `infra/nginx/html/`
7. `README.md`
8. `docs/README.md`
9. `docs/adrs/0071-borda-local-nginx-https.md`
10. `docs/adrs/0072-load-balance-local-ledger-nginx.md`
11. `docs/architecture/`

Use `docs/adrs/` para entender decisoes ja tomadas. Nao reescreva ADR historica como documentacao atual.

# Regras obrigatorias

- Preserve o Nginx como overlay opcional local, nao como dependencia obrigatoria da stack minima.
- Preserve as portas HTTP diretas das APIs, salvo pedido explicito em contrario.
- Preserve HTTP interno entre Nginx e APIs em `8080`, salvo decisao arquitetural explicita.
- Preserve HTTPS somente na borda local em `7443`.
- Nao versione certificados locais ou arquivos sensiveis de ambiente.
- Nao adicione HSTS na borda local. Em `localhost` e subdominios `.localhost`, HSTS pode atrapalhar rollback ou testes locais.
- Preserve subdominios `.localhost` para as APIs, evitando reescrita por path e evitando exigir `PathBase` nas APIs.
- Preserve `/swagger` como entrada amigavel via Nginx e `/swagger/v1/swagger.json` como documento OpenAPI das APIs.
- Nao aplique CSP nos hosts de API/Swagger via Nginx sem validar a Swagger UI. CSP deve ficar restrita ao portal estatico, salvo decisao explicita.
- Preserve `Cache-Control: no-store` nos hosts de API via Nginx.
- Preserve propagacao de `X-Correlation-Id` e `X-Forwarded-*`.
- Preserve `server_tokens off` e a reducao de fingerprinting quando alterar a imagem ou headers.
- Preserve `least_conn` no upstream `ledger_api`, salvo mudanca explicita do criterio de balanceamento.
- Preserve `X-Upstream-Addr` apenas como diagnostico local, evitando transformar esse header em contrato publico.
- Antes de adicionar `add_header` em `location`, revise a regra de heranca do Nginx.
- Evite `if` dentro de `location`, a menos que a alternativa seja pior e a semantica esteja claramente justificada.
- Confirme o contexto correto das diretivas Nginx: `main`, `events`, `http`, `server`, `location` ou `upstream`.
- Nao assuma que toda diretiva aceita variaveis. Verifique antes de usar variaveis em diretivas de proxy, TLS, cache, log ou limites.
- Mantenha logs uteis para diagnostico local, especialmente `correlation_id`, `upstream_addr`, `upstream_status`, status e tempos.
- Se alterar limites defensivos, documente impacto esperado em respostas `413` e `429`.
- Se alterar topologia, subdominios, upstreams, portas, TLS, headers ou fluxo de acesso, avalie atualizacao de README, docs, LikeC4 e ADR.
- Se a mudanca afetar testes k6 ou scripts que usam portas HTTP diretas, atualize os arquivos correspondentes ou explique por que nao ha impacto.

# Passos

1. Leia `AGENTS.md` e identifique se a tarefa e realmente de Nginx local.
2. Leia os arquivos Nginx e Compose relevantes antes de propor alteracao.
3. Consulte ADR-0071 e ADR-0072 quando a mudanca tocar HTTPS local, subdominios, load balance, upstream ou optionalidade da borda.
4. Classifique a alteracao como configuracao Nginx, Compose, documentacao, arquitetura ou teste.
5. Faca a menor mudanca possivel.
6. Preserve compatibilidade com a stack minima sem Nginx.
7. Valide sintaxe e comportamento localmente quando possivel.
8. Atualize documentacao apenas quando houver mudanca observavel de uso, contrato, topologia ou decisao arquitetural.
9. Revise o diff antes de finalizar.
10. Gere commit semantico, conforme `AGENTS.md`, quando houver alteracao no repositorio.

# Validacao recomendada

Quando a alteracao envolver somente arquivos Nginx ou Compose, prefira validacoes proporcionais:

- `docker compose -f compose.yaml -f compose.nginx.yaml config`
- `docker compose -f compose.yaml -f compose.nginx.yaml build nginx-edge`
- `docker compose -f compose.yaml -f compose.nginx.yaml run --rm nginx-edge nginx -t`

Quando a stack local estiver disponivel, valide portal, Swaggers via subdominios `.localhost` e headers relevantes da borda.

# Saidas esperadas

- Explicacao curta da mudanca e do motivo.
- Arquivos alterados listados de forma objetiva.
- Validacoes executadas ou motivo claro para nao executar.
- Impacto em README, docs, LikeC4, ADRs e testes avaliado explicitamente.
- Riscos residuais informados quando a mudanca tocar TLS, headers, cache, limites ou roteamento.

# Restricoes

- Nao copiar configuracoes de producao sem adaptar ao escopo local da POC.
- Nao transformar recomendacoes genericas de Nginx em regra absoluta se conflitarem com ADRs locais.
- Nao remover controles de seguranca existentes sem justificativa explicita.
- Nao adicionar dependencia externa ou modulo Nginx novo sem justificar necessidade, imagem, suporte e impacto de build.
- Nao introduzir scripts nao determinismos ou comandos destrutivos.
- Nao alterar codigo de producao das APIs para contornar problema que pertence ao Nginx, salvo decisao explicita e documentada.
