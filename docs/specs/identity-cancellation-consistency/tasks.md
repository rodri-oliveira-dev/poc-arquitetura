# Tasks

- [x] Inspecionar `CreateUserCommandHandler`, `IdempotencyService`,
  repositorios, `IdentityDbContext`, `KeycloakAdminClient`, testes, ADRs e docs.
- [x] Confirmar o risco real de cancelamento depois do efeito externo e antes da
  persistencia local.
- [x] Criar estado explicito de execucao no handler de cadastro.
- [x] Configurar timeout de compensacao independente do token da requisicao.
- [x] Compensar cancelamento interno do `KeycloakAdminClient` apos criacao do
  usuario e antes da senha.
- [x] Compensar em `OperationCanceledException` quando o usuario externo ja foi
  criado e o commit local nao foi confirmado.
- [x] Preservar ausencia de compensacao antes do efeito externo.
- [x] Ajustar idempotencia para marcar falha mesmo quando o token HTTP estiver
  cancelado.
- [x] Manter retry automatico apenas para falhas recuperaveis.
- [x] Adicionar testes unitarios para cancelamento antes do Keycloak.
- [x] Adicionar testes unitarios para cancelamento depois do Keycloak.
- [x] Adicionar testes unitarios para cancelamento durante `SaveChangesAsync`.
- [x] Adicionar testes unitarios para falha de persistencia, compensacao
  bem-sucedida e compensacao falha.
- [x] Adicionar testes para operacao local confirmada sem compensacao.
- [x] Adicionar testes para `Processing` expirado/retry sem duplicidade.
- [x] Executar validacoes proporcionais.
- [x] Revisar diff final e criar commit semantico.
