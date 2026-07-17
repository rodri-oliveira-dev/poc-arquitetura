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

## Entrega

- [x] Revisar diff.
- [x] Registrar riscos residuais.
- [x] Criar commit semantico.
