# Conclusao da padronizacao temporal com TimeProvider - Tasks

- [x] Inventariar usos temporais obrigatorios em codigo de producao.
- [x] Classificar usos por regra, timestamp funcional, persistencia, integracao, retry/lease, telemetria, framework, migration, teste e mantidos.
- [x] Remover fallback temporal de `IdentityService.Domain.Users.User.Register`.
- [x] Passar timestamp fixo da Application para o aggregate `User`.
- [x] Remover fallback temporal de `FunctionalAuditRecord.Create`.
- [x] Passar `createdAt` da Application para auditoria funcional.
- [x] Injetar `TimeProvider` em `FakePaymentGateway`.
- [x] Injetar `TimeProvider` em `StripePaymentGateway`.
- [x] Preservar `created` da Stripe quando presente.
- [x] Controlar fallback de `created` da Stripe quando ausente.
- [x] Migrar delays de retry/backoff relevantes para overload com `TimeProvider`.
- [x] Ajustar testes com instantes constantes.
- [x] Validar builds e testes contextuais.
- [x] Documentar usos migrados, mantidos e riscos residuais.
