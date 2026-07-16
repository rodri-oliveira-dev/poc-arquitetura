# Tasks

- [x] Diagnosticar politica `fixed` global e ordem atual do pipeline.
- [x] Mapear claims JWT usadas por subject, client, scope e merchant.
- [x] Mapear endpoints autenticados, webhook anonimo, administracao, health e
  readiness.
- [x] Especificar requisitos e criterios de aceitacao.
- [x] Definir design de policies, chaves de particao, options e metricas.
- [x] Implementar policies particionadas em `ApiDefaults`.
- [x] Ajustar pipeline para executar `UseRateLimiter()` apos autenticacao.
- [x] Aplicar policies de leitura, escrita, administracao e webhook nas APIs.
- [x] Adicionar testes de isolamento por subject, merchant, cliente e IP.
- [x] Adicionar testes de `429`, `Retry-After`, health e metricas de baixa
  cardinalidade.
- [x] Atualizar documentacao SDD e indice.
- [x] Executar validacoes proporcionais finais.
- [x] Revisar diff final.
- [x] Criar commit semantico.
