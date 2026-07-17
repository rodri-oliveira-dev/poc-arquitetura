# Tasks

- [x] Inspecionar handler, estado de execucao, opcoes de consistencia, portas,
  Keycloak admin client, dominio, repositorios, idempotencia, testes e docs.
- [x] Confirmar a sequencia anterior e a janela sem compensacao.
- [x] Definir requisitos verificaveis e criterios de aceitacao.
- [x] Elaborar plano tecnico curto.
- [x] Mover operacoes pos-Keycloak para a regiao compensavel.
- [x] Cobrir falha antes da criacao no Keycloak.
- [x] Cobrir cancelamento antes do efeito externo.
- [x] Cobrir falha no `IMerchantIdGenerator`.
- [x] Cobrir falha ao construir `MerchantId`.
- [x] Cobrir falha ao construir `Email`.
- [x] Cobrir falha ao construir `Username`.
- [x] Preservar cobertura de falhas em `AddAsync` e `SaveChangesAsync`.
- [x] Cobrir cancelamento durante `AddAsync`.
- [x] Preservar cobertura de cancelamento durante `SaveChangesAsync`.
- [x] Cobrir compensacao bem-sucedida.
- [x] Cobrir compensacao que falha.
- [x] Cobrir compensacao que excede timeout.
- [x] Cobrir persistencia local confirmada sem compensacao.
- [x] Preservar retry idempotente antes do efeito externo.
- [x] Cobrir retry idempotente apos compensacao confirmada.
- [x] Cobrir bloqueio de retry apos compensacao falha.
- [x] Preservar cobertura de retry concorrente com PostgreSQL real.
- [x] Preservar replay concluido sem repetir efeitos externos.
- [x] Executar validacoes.
- [x] Atualizar relatorio final com resultados reais.
