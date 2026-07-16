# IdentityService: consistencia em cancelamentos de cadastro

## Contexto

O cadastro de usuarios do `IdentityService` executa dois efeitos relevantes:

1. cria o usuario no Keycloak;
2. persiste o vinculo local no PostgreSQL.

O diagnostico confirmou que a implementacao ja possuia idempotencia opcional,
constraint unica em PostgreSQL e compensacao do Keycloak em falhas comuns de
persistencia. O risco restante estava nos cancelamentos: quando o
`CancellationToken` da requisicao era cancelado depois da criacao externa, os
`catch` filtrados por `!cancellationToken.IsCancellationRequested` deixavam a
operacao escapar sem compensar e sem marcar o registro idempotente como `Failed`.

## Requisitos verificaveis

- Distinguir explicitamente os estagios: usuario externo ainda nao criado,
  usuario criado no Keycloak, persistencia local iniciada e persistencia local
  confirmada.
- Compensar o usuario no Keycloak quando o efeito externo ja ocorreu e a
  persistencia local nao foi confirmada.
- Executar a compensacao com token independente do token HTTP da requisicao.
- Limitar a compensacao por timeout configuravel no caso de uso e no client
  administrativo do Keycloak.
- Nunca remover o usuario do Keycloak quando a persistencia local tiver sido
  confirmada.
- Tratar cancelamento antes do efeito externo como falha sem compensacao.
- Tratar cancelamento durante persistencia local como falha pos-efeito externo,
  com compensacao.
- Evitar que registros idempotentes permanecam em `Processing` indefinidamente
  depois de cancelamento do token HTTP.
- Preservar retry deterministicamente quando a falha ocorreu antes de efeito
  externo ou depois de compensacao confirmada.
- Bloquear retry automatico quando a compensacao do Keycloak tambem falhar.
- Preservar a regra de nao armazenar ou registrar senhas.
- Manter e-mail de boas-vindas como best effort pos-commit, fora da transacao
  distribuida.
- Registrar logs estruturados sem expor `Idempotency-Key`, senha, token ou
  payload sensivel.
- Nao introduzir Saga generica, Outbox ou Worker novo para este fluxo.

## Criterios de aceitacao

- Cancelamento antes da criacao no Keycloak nao chama `DeleteUserAsync`.
- Cancelamento depois da criacao no Keycloak e antes do commit local chama
  `DeleteUserAsync` com token diferente do token HTTP.
- Cancelamento durante `SaveChangesAsync` marca a chave idempotente como
  `Failed` e `AfterIdentityProviderCompensated` quando a compensacao conclui.
- Falha de persistencia depois da criacao no Keycloak compensa e preserva a
  excecao original.
- Falha de compensacao registra `AfterIdentityProviderCompensationFailed` e nao
  mascara a excecao original.
- Operacao local confirmada fica `Completed` e nao compensa.
- Registro `Processing` abandonado por cancelamento vira `Failed` ou expira por
  lock/TTL, sem bloquear indefinidamente.
- Retry concorrente de falha recuperavel e claim atomico: apenas uma chamada
  reexecuta.
- Replay com a mesma `Idempotency-Key` nao cria usuario duas vezes.

## Fora do escopo

- Alterar contrato HTTP.
- Tornar o `IdentityService` um IdP.
- Alterar o papel do Keycloak como emissor de tokens.
- Criar recuperacao operacional automatica para compensacao que falhou.
- Persistir ou comparar senha.
- Tornar e-mail de boas-vindas duravel.
