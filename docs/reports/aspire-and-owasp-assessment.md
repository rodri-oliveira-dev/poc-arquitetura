# Avaliacao de adocao do .NET Aspire e riscos OWASP

## Resumo executivo

Esta avaliacao analisou a viabilidade de adotar .NET Aspire na POC e revisou riscos de seguranca sob OWASP API Security Top 10 e OWASP Top 10. Nao houve implementacao de codigo, refatoracao ou correcao de vulnerabilidades.

A adocao do Aspire foi classificada como **media complexidade**. O projeto ja tem uma topologia local clara em `compose.yaml`, tres APIs .NET, dois bancos PostgreSQL, Kafka, Outbox, DLQ, health/readiness e OpenTelemetry opcional. Isso favorece uma adocao incremental com `AppHost` e `ServiceDefaults`, mas ha custo relevante para evitar drift com compose, padronizar configuracao, decidir fronteiras de observabilidade e adaptar CI/CD sem tratar Aspire como plataforma de producao.

Na revisao OWASP foram encontrados **0 achados criticos, 4 altos, 8 medios e 3 baixos**. Os principais riscos sao: credenciais e usuarios de POC em configuracao versionada, autorizacao sem vinculo entre usuario/token e `merchantId`, dependencias NuGet com avisos de vulnerabilidade, Auth.Api com hardening inferior ao das APIs de negocio, exposicao publica de Swagger/health/readiness e transporte local sem TLS/autenticacao para Kafka e bancos.

## Classificacao de complexidade do Aspire

**Classificacao final: media complexidade.**

Justificativa:

- A solucao tem tres processos HTTP (`Auth.Api`, `LedgerService.Api`, `BalanceService.Api`) e duas responsabilidades em background embutidas (`OutboxKafkaPublisherService` e `LedgerEventsConsumer`).
- A stack local depende de PostgreSQL, Kafka/KRaft, criacao de topicos, JWKS, portas expostas e overrides por variaveis de ambiente.
- Ja existe compose funcional e documentado; Aspire agregaria valor para orquestracao local, service discovery, dashboard e defaults de observabilidade, mas tambem criaria uma segunda forma de executar a stack.
- `ServiceDefaults` tende a exigir alteracoes pequenas nos projetos de API, mas transversais. Essas alteracoes precisam preservar hardening, JWT/JWKS, CORS, Swagger, readiness e configuracao atual.
- O ganho em producao e indireto: Aspire AppHost e dashboard sao principalmente experiencia de desenvolvimento/local orchestration. Producao ainda exigiria desenho proprio para container runtime, secrets, TLS, Kafka gerenciado, banco gerenciado, observabilidade e pipelines.

Referencias externas consultadas:

- Microsoft Learn, .NET Aspire overview: https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview
- Microsoft Learn, .NET Aspire orchestration overview: https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview
- Microsoft Learn, service defaults: https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults

## Contexto atual encontrado

Componentes principais:

- `src/Auth.Api`: emite JWT RS256, publica JWKS em `/.well-known/jwks.json`, usa usuario/senha fixos de POC e persiste chave RSA em arquivo.
- `src/LedgerService.Api`: endpoint de escrita `POST /api/v1/lancamentos`, JWT Bearer via JWKS, scopes, CORS, rate limiting, security headers, ProblemDetails, `/health`, `/ready`.
- `src/LedgerService.Infrastructure`: EF Core/PostgreSQL, Outbox, Kafka producer com idempotencia habilitada, headers de evento e correlacao.
- `src/BalanceService.Api`: endpoints de leitura de consolidados, JWT Bearer via JWKS, scopes, CORS, rate limiting, security headers, ProblemDetails, `/health`, `/ready`.
- `src/BalanceService.Infrastructure`: Kafka consumer, processamento idempotente por evento, DLQ e persistencia de projecao em PostgreSQL.
- `compose.yaml`: PostgreSQL Ledger, PostgreSQL Balance, Kafka single node KRaft, job de init de topicos, Ledger, Balance e Auth.
- `.github/workflows`: CI .NET com restore/build/test/coverage, CodeQL e dependency review.

ADRs relevantes existentes:

- ADR-0003: Kafka com Outbox.
- ADR-0004: JWT RS256 via JWKS.
- ADR-0005: correlation id e base para OpenTelemetry.
- ADR-0015: resiliencia para APIs/JWKS ainda proposta.
- ADR-0017: DLQ, versionamento de eventos e readiness operacional aceitos.

## Beneficios esperados com Aspire

- Orquestracao local tipada para APIs, bancos e Kafka, reduzindo parte da friccao de comandos manuais.
- Dashboard local para visualizar recursos, logs, traces e health checks durante desenvolvimento.
- `ServiceDefaults` para padronizar health checks, OpenTelemetry, resiliencia HTTP e discovery entre servicos, desde que adaptado ao hardening ja existente.
- Menor custo de onboarding para novos devs quando comparado a memorizar portas, ordem de subida, topicos e overrides do compose.
- Melhor documentacao viva da topologia local, desde que `AppHost` seja tratado como complemento ao compose ou substituto deliberado.

## Riscos e custos de adocao

- Drift entre `compose.yaml`, README, scripts, VS Code e `AppHost`.
- Necessidade de decidir se Aspire gerencia tambem PostgreSQL/Kafka ou apenas as APIs e dependencias ja externas.
- Possivel duplicacao de health/readiness e OpenTelemetry se `ServiceDefaults` for adicionado sem consolidar os defaults atuais.
- Curva de aprendizado para equipe em `AppHost`, resource graph, parametros, secrets locais e dashboard.
- Ajustes em CI/CD para validar que a solucao continua buildando sem exigir o AppHost como pre-requisito.
- Risco de confundir Aspire local orchestration com desenho de producao. O AppHost nao deve virar o plano de deploy produtivo por si so.

## Impactos em desenvolvimento local

Impacto positivo:

- O fluxo local poderia subir Auth, Ledger, Balance, PostgreSQL e Kafka por um unico AppHost.
- O dashboard facilitaria diagnostico de logs, health e traces.

Pontos de atencao:

- Hoje o README e `compose.yaml` usam `nerdctl compose`, portas fixas e migrations manuais. O Aspire exigiria documentar claramente um novo caminho ou substituir o atual.
- O Kafka atual usa imagem Apache Kafka com KRaft e init de topicos. A modelagem no AppHost precisa preservar criacao explicita de `ledger.ledgerentry.created` e `ledger.ledgerentry.created.dlq`.
- As connection strings e `Jwt__JwksUrl` tem semantica diferente fora e dentro de container. O Aspire deve centralizar esses parametros para reduzir divergencia.

## Impactos em testes de integracao

- Os testes atuais usam `WebApplicationFactory` e Testcontainers PostgreSQL em parte do escopo. Nao ha evidencia de que precisem depender de Aspire.
- A recomendacao e manter testes de integracao independentes do AppHost, usando factories/Testcontainers, para preservar velocidade e isolamento.
- Pode haver um novo teste smoke opcional para o AppHost, mas ele deve ficar separado do caminho rapido de CI.

## Impactos em CI/CD

- O pipeline atual restaura, compila e testa `LedgerService.slnx`. Adicionar projetos `*.AppHost` e `*.ServiceDefaults` exige incluir esses projetos na solucao e garantir restore/build.
- Builds de imagem e compose atual nao parecem ser executados no CI. Se Aspire entrar, decidir se o CI valida apenas build do AppHost ou tambem sobe a topologia.
- Dependency review e CodeQL ja existem; faltam gates explicitos para `dotnet list package --vulnerable`, imagens de container e configuracao de secrets.

## Impactos em producao

- Aspire pode ajudar na padronizacao de telemetria e configuracao, mas nao elimina a necessidade de desenho produtivo para:
  - TLS/HTTPS externo e interno;
  - secrets manager;
  - Kafka autenticado/autorizado;
  - bancos com usuarios de menor privilegio, backup e HA;
  - limites de recursos e replicas;
  - readiness/liveness integrados ao orquestrador real;
  - coleta centralizada de logs, traces e metricas.
- O compose atual usa ambiente `Development` nos containers e credenciais de POC. Isso deve ser tratado antes de qualquer conversa de producao.

## Pre-requisitos para adocao segura

- Definir se Aspire sera o fluxo principal local ou apenas alternativa experimental.
- Criar ADR especifico para escopo do AppHost e relacao com compose.
- Definir politica de secrets locais e produtivos.
- Padronizar exposicao de Swagger, CORS, `/health` e `/ready` por ambiente.
- Resolver ou aceitar formalmente os avisos NuGet de vulnerabilidade.
- Definir ownership de merchant/tenant no token ou em autorizacao de dominio.
- Garantir que `ServiceDefaults` nao reduza hardening ja presente nas APIs de negocio.

## Achados OWASP

| id       | categoria                                                              | severidade | evidencia no projeto                                                                                                                                                            | impacto                                                                                     | recomendacao                                                                                                        | prioridade |
| -------- | ---------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ---------- |
| OWASP-01 | API1/Broken Object Level Authorization; Broken Access Control          | Alta       | `merchantId` vem do body/query em `LancamentosController` e `ConsolidadosController`; tokens validam scopes, mas nao ha evidencia de vinculo usuario/audience/claim ao merchant | Um token com scope valido pode criar ou consultar dados de qualquer merchant informado      | Definir modelo de tenancy/autorizacao por merchant e validar claim/relacao no Application/Domain ou policy dedicada | P1         |
| OWASP-02 | API2/Broken Authentication; Identification and Authentication Failures | Alta       | `Auth.Api` usa usuario/senha fixos (`poc-usuario`/`Poc#123`) e concede todos os scopes quando `scope` e vazio                                                                   | Inadequado fora da POC; facilita abuso de fluxos sensiveis e tokens amplos                  | Manter como POC apenas ou substituir por IdP/OIDC; exigir scopes explicitos; adicionar lockout/rate limit/auditoria | P1         |
| OWASP-03 | Vulnerable and Outdated Components                                     | Alta       | `dotnet list ... --vulnerable` apontou `System.Security.Cryptography.Xml 9.0.0` com avisos altos transitivos nos projetos Infrastructure                                        | Exposicao a CVEs conhecidas em cadeia de dependencias                                       | Avaliar origem transitiva, atualizar pacote raiz/override centralizado ou registrar excecao temporaria com prazo    | P1         |
| OWASP-04 | Vulnerable and Outdated Components                                     | Media      | `OpenTelemetry.Api 1.15.0` aparece como pacote top-level vulneravel moderado nos tres projetos de API                                                                           | Supply chain com CVE conhecida; risco depende da superficie exploravel                      | Atualizar versao centralizada ou alinhar pacote com versao corrigida; adicionar gate NuGet no CI                    | P2         |
| OWASP-05 | Security Misconfiguration; API9 Improper Inventory Management          | Media      | `UseApiSwagger()` e chamado sempre em Ledger/Balance; Auth habilita Swagger em Development ou `Swagger:Enabled=true`, e compose define `Swagger__Enabled=true`                  | OpenAPI publico amplia inventario e descoberta de endpoints                                 | Condicionar Swagger por ambiente/config e exigir decisao explicita para ambientes compartilhados/produtivos         | P2         |
| OWASP-06 | Security Misconfiguration                                              | Alta       | `compose.yaml` e `appsettings.json` contem senhas de banco e credenciais de POC; README tambem documenta valores reais de POC                                                   | Risco de reutilizacao acidental e vazamento de padroes em ambientes nao locais              | Substituir por placeholders, `.env` nao versionado ou user-secrets; documentar segregacao local/producao            | P1         |
| OWASP-07 | Cryptographic Failures; Security Misconfiguration                      | Media      | JWKS usa `http://...`, `RequireHttpsMetadata=false`; Kafka usa `PLAINTEXT`; compose expoe bancos/Kafka no host                                                                  | Dados e tokens trafegam sem TLS no ambiente local; risco alto se replicado                  | Manter explicitamente restrito a local; exigir TLS/HTTPS/SASL/SSL em ambientes compartilhados/produtivos            | P2         |
| OWASP-08 | API4 Unrestricted Resource Consumption                                 | Media      | Rate limit fixo existe em Ledger/Balance, mas nao ha limite explicito de tamanho de request/body; Balance nao limita intervalo maximo de periodo                                | Requisicoes grandes ou intervalos extensos podem consumir CPU/DB                            | Definir limites Kestrel/form/body, max range de datas e limites por rota/cliente                                    | P2         |
| OWASP-09 | Security Misconfiguration                                              | Media      | Auth.Api nao possui o mesmo conjunto de `SecurityHeadersMiddleware`, rate limiting, HTTPS redirection/HSTS, ProblemDetails e Swagger gating das APIs de negocio                 | Superficie de autenticacao menos endurecida que os servicos consumidores                    | Padronizar hardening minimo tambem no Auth.Api ou mover para IdP externo                                            | P2         |
| OWASP-10 | Software and Data Integrity Failures; Insecure Design                  | Media      | `CreateLancamentoRequest.Amount` usa `double`; o dominio persiste `decimal` depois de converter string gerada a partir de double                                                | Risco de precisao/representacao em dados monetarios                                         | Alterar contrato futuro para string/decimal com validacao de escala; documentar migracao de contrato                | P2         |
| OWASP-11 | API7/SSRF; Unsafe Consumption of APIs                                  | Media      | `Jwt:JwksUrl` e configuravel; retriever aceita URL sem allowlist e usa `HttpClient` direto                                                                                      | Configuracao maliciosa pode direcionar fetch para destino interno; risco operacional/config | Validar scheme/host por ambiente, preferir OIDC metadata confiavel e `HttpClientFactory` com policy                 | P2         |
| OWASP-12 | Security Logging and Monitoring Failures                               | Baixa      | OpenTelemetry vem desabilitado por padrao; README referencia `docs/observability.md`, mas o arquivo nao foi encontrado                                                          | Diagnostico e auditoria podem ficar incompletos em incidentes                               | Criar doc de observabilidade e padronizar OTLP/logs/metricas por ambiente                                           | P3         |
| OWASP-13 | Security Misconfiguration; Container Security                          | Media      | Dockerfiles finais nao declaram usuario nao-root; imagens usam tags (`10.0`, `postgres:16`, `apache/kafka:3.7.0`) sem digest; compose sem limites de recurso                    | Risco de hardening de runtime e supply chain de imagens                                     | Definir baseline de containers: usuario nao-root, tags/digests, scan de imagem, resource limits                     | P2         |
| OWASP-14 | API6 Unrestricted Access to Sensitive Business Flows                   | Baixa      | Login e criacao de lancamento tem rate limit generico nas APIs de negocio; Auth.Api nao tem rate limit dedicado                                                                 | Fluxos sensiveis podem exigir protecao mais especifica                                      | Adotar limites por endpoint, IP/cliente/usuario e monitoramento de abuso                                            | P3         |
| OWASP-15 | Improper Inventory Management                                          | Baixa      | README aponta `docs/observability.md`, ausente na estrutura inspecionada                                                                                                        | Inventario/documentacao operacional incompleto                                              | Criar ou corrigir referencia da documentacao de observabilidade                                                     | P3         |

## Quick wins

- Condicionar Swagger de Ledger/Balance por ambiente/config, como ja existe parcialmente no Auth.Api.
- Adicionar rate limiting e security headers no Auth.Api.
- Registrar gate de vulnerabilidades NuGet no CI ou um comando documentado de verificacao.
- Trocar exemplos versionados de senha por placeholders e `.env.example`.
- Definir limite maximo para consulta por periodo no BalanceService.
- Criar `docs/observability.md` ou corrigir referencia no README.

## Recomendacoes estruturais

- Evoluir autenticacao para IdP OIDC ou Keycloak conforme ADR-0006, removendo credenciais fixas de POC dos fluxos reais.
- Introduzir autorizacao por tenant/merchant antes de expor a API a usuarios ou integracoes reais.
- Padronizar defaults de API compartilhados sem enfraquecer controles existentes: ProblemDetails, headers, rate limits, CORS, Swagger, OpenTelemetry e health checks.
- Formalizar politica de secrets e configuracao por ambiente.
- Definir baseline de seguranca para Kafka, bancos e containers.
- Tratar dados monetarios como decimal/string no contrato HTTP em uma versao futura.

## Plano sugerido de adocao em fases

1. **Fase 0 - Decisao e preparo**
   - Aprovar ADR de adocao incremental do Aspire.
   - Definir se compose continua como fonte principal local ou se AppHost passa a ser o caminho recomendado.
   - Resolver segredos de POC e exposicoes operacionais mais evidentes.

2. **Fase 1 - ServiceDefaults experimental**
   - Criar projeto `ServiceDefaults` com observabilidade e health checks.
   - Integrar em uma API primeiro, validando que nao ha regressao de JWT, Swagger, rate limiting e headers.

3. **Fase 2 - AppHost local**
   - Modelar Auth, Ledger, Balance, PostgreSQL, Kafka e topicos.
   - Preservar migrations manuais ou criar decisao explicita para job de migration.
   - Atualizar README com um unico fluxo recomendado e um fluxo alternativo.

4. **Fase 3 - Telemetria e resiliencia**
   - Padronizar OTLP/exporters, resource names, traces HTTP/Kafka e metricas.
   - Migrar JWKS/HTTP externo para `HttpClientFactory` e policy de timeout/retry/circuit breaker.

5. **Fase 4 - CI/CD e producao**
   - Buildar AppHost/ServiceDefaults no CI.
   - Adicionar scans NuGet/container.
   - Documentar claramente que producao requer orquestrador e controles proprios, nao apenas AppHost.

## Pontos que precisam de decisao arquitetural

- Aspire substitui ou complementa `compose.yaml`?
- Quem e a fonte de verdade para topologia local: README, compose ou AppHost?
- Auth.Api continua como POC ou sera substituido por Keycloak/OIDC antes de endurecer seguranca?
- O token tera claims de merchant/tenant? Onde a autorizacao de objeto sera aplicada?
- Swagger, `/health` e `/ready` serao publicos em quais ambientes?
- Qual padrao de secrets sera usado localmente, em CI e em producao?
- Como tratar vulnerabilidades NuGet e imagens: bloquear build ou gerar excecao com prazo?

## Limitacoes da analise

- Analise estatica e arquitetural; nao houve pentest, fuzzing, SAST completo local, DAST ou execucao da stack.
- A consulta de vulnerabilidades NuGet foi executada para projetos principais de API e evidenciou avisos tambem em projetos transitivos; a remediacao nao foi implementada.
- Nao houve inspecao de permissoes reais de banco em um ambiente provisionado.
- Nao houve validacao de TLS, rede ou policies fora do `compose.yaml`.
- Os achados diferenciam riscos reais observados de riscos potenciais; alguns itens sao aceitaveis para POC local, mas devem ser decididos antes de ambientes compartilhados/produtivos.
