# Revisao da experiencia de documentacao - tarefas

## Inventario e diagnostico

- [x] Listar arquivos Markdown do repositorio.
- [x] Extrair titulo, tamanho aproximado e links enviados.
- [x] Identificar documentos de entrada, guias atuais, ADRs, specs, runbooks e relatorios.
- [x] Buscar referencias antigas ou contraditorias sobre `Auth.Api`, `AuditService`, `PaymentService`, Kafka/Pub/Sub e OpenAPI.
- [x] Conferir estrutura de `src`, `tests`, solutions, scripts e workflows relevantes.

## Especificacao e design

- [x] Criar `requirements.md`.
- [x] Criar `design.md`.
- [x] Definir taxonomia documental.
- [x] Definir jornadas de leitura.

## Revisao e reestruturacao

- [x] Reescrever README da raiz como porta de entrada.
- [x] Reestruturar `docs/README.md` como mapa de navegacao.
- [x] Registrar a revisao em spec SDD.
- [x] Atualizar documentos com contradicoes claras sobre AuditService e historico.
- [x] Atualizar relatorio final da revisao.

## Validacao

- [x] Validar links relativos Markdown por script local.
- [x] Executar `git diff --check`.
- [x] Executar validacao de ADRs.
- [x] Executar validacao de eventos.
- [x] Executar build LikeC4.
- [x] Executar lint OpenAPI.
- [x] Executar geracao OpenAPI.
- [x] Executar stack local padrao.
- [x] Executar k6 smoke Kafka.
- [x] Executar OWASP ZAP local.
- [x] Registrar validacoes nao executadas ou nao aplicaveis.

## Validacao complementar solicitada

- [x] Validar estrutura, variaveis locais, Compose, certificados e ferramentas antes dos fluxos.
- [x] Executar `./test.ps1` completo.
- [x] Executar k6 ampliado para Kafka, incluindo `load-kafka`, `ledger-load-kafka`, `transfer-smoke-kafka` e `transfer-load-kafka`.
- [x] Subir Nginx local com HTTPS e validar portal, Ledger e Balance via `*.localhost`.
- [x] Executar OWASP ZAP autenticado com active scan em Ledger e Balance.
- [x] Validar subida do overlay de observabilidade e registrar falhas de permissao em Prometheus e Loki.
- [x] Validar Pub/Sub legado ate o ponto possivel e registrar backlog de Outbox como impeditivo do fluxo Ledger -> Balance em 120s.
- [x] Reexecutar `transfer-fullstack-kafka` apos drenagem de backlog e registrar que a Saga completou, mas o pos-check Kafka falhou ao localizar o evento esperado.

## Tarefas tecnicas derivadas das validacoes

- [ ] Corrigir `scripts/local/start-full-stack.ps1` para funcionar com o formato de `docker ps --format` no PowerShell/Docker atual.
- [ ] Corrigir `scripts/local/start-stack-pubsub.ps1` para repassar `-MessagingProvider PubSub`, `-OverlayFile` e flags opcionais ao `start-stack.ps1` sem erro de binding.
- [ ] Ajustar o overlay de observabilidade para que Prometheus consiga criar `/prometheus/queries.active` e Loki consiga criar `/loki/rules` no `tmpfs`.
- [ ] Investigar o pos-check de Kafka do `transfer-fullstack-kafka`, que falhou ao procurar `transfer.transferencia.solicitada` mesmo com a Saga concluida.
- [ ] Criar ou documentar um modo de validacao Pub/Sub com estado controlado, para evitar que backlog antigo de Outbox torne o smoke inconclusivo.
- [ ] Avaliar se os scripts de validacao devem carregar `.env.local` explicitamente em chamadas `docker compose exec`, evitando falha quando variaveis obrigatorias existem no arquivo mas nao no ambiente do processo.

## Entrega

- [x] Revisar diff.
- [x] Registrar riscos residuais.
- [x] Criar commit semantico.
