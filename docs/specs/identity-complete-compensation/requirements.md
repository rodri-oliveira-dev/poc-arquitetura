# IdentityService: compensacao completa apos efeito externo

## Contexto

O cadastro de usuarios do `IdentityService` cria o usuario no Keycloak antes de
persistir o agregado local no PostgreSQL. A implementacao ja compensava falhas
durante `AddAsync`, `SaveChangesAsync` e cancelamentos pos-efeito externo, mas
o diagnostico confirmou uma janela entre a confirmacao da criacao no Keycloak e
a entrada no bloco `try/catch` responsavel pela compensacao.

Sequencia anterior confirmada:

1. criar usuario no Keycloak;
2. marcar usuario externo como criado;
3. gerar `MerchantId`;
4. construir `MerchantId`, `Email` e `Username`;
5. criar o agregado `User` e `UserRegisteredDomainEvent`;
6. entrar no `try/catch`;
7. persistir no PostgreSQL.

Falhas nos passos 3 a 5 podiam deixar usuario externo sem vinculo local.

## Requisitos verificaveis

- Considerar a criacao confirmada no Keycloak como inicio da regiao que exige
  compensacao.
- Envolver na regiao compensavel todas as operacoes posteriores ao efeito
  externo e anteriores a confirmacao local.
- Tentar remover o usuario no Keycloak quando qualquer excecao ocorrer depois
  da criacao externa e antes do commit local confirmado.
- Nunca compensar depois que a persistencia local tiver sido confirmada.
- Usar token independente do token HTTP cancelado para compensacao.
- Limitar a compensacao por
  `IdentityService:CreateUserConsistency:CompensationTimeout`.
- Preservar a excecao original quando a compensacao tambem falhar ou exceder o
  timeout.
- Registrar falha de compensacao de forma estruturada, sem senha, token,
  `Idempotency-Key` integral ou payload sensivel.
- Manter o e-mail de boas-vindas fora da regiao de consistencia distribuida.
- Preservar replay, retry e conflito idempotente existentes.
- Permitir retry automatico quando a falha ocorreu antes do efeito externo ou
  depois de compensacao confirmada.
- Bloquear retry automatico quando a compensacao falhou ou ficou com resultado
  desconhecido.
- Nao introduzir Saga generica, Outbox ou Worker para esse fluxo.
- Nao alterar contratos HTTP, scopes, policies ou o papel do Keycloak como
  emissor de tokens.

## Criterios de aceitacao

- Falha antes da criacao no Keycloak nao chama compensacao.
- Cancelamento antes do efeito externo nao chama compensacao.
- Falha em `IMerchantIdGenerator` apos criacao no Keycloak chama compensacao.
- Falha na construcao de `MerchantId`, `Email` ou `Username` chama compensacao.
- Falha durante registro do agregado permanece dentro da regiao compensavel.
- Falhas em `AddAsync` e `SaveChangesAsync` continuam compensando.
- Cancelamentos em `AddAsync` e `SaveChangesAsync` compensam com token proprio.
- Compensacao bem-sucedida classifica falha idempotente como recuperavel.
- Compensacao com falha ou timeout nao mascara a excecao original e bloqueia
  retry automatico.
- Persistencia local confirmada nao executa compensacao.
- Replay concluido nao repete Keycloak, `MerchantId`, persistencia local ou
  e-mail.
- Retry concorrente de falha recuperavel continua protegido por claim atomico
  em PostgreSQL.

## Fora do escopo

- Alterar contrato HTTP de `POST /api/v1/users`.
- Persistir, comparar ou registrar senha.
- Automatizar reconciliacao operacional de usuario externo orfao.
- Tornar o envio de e-mail duravel.
- Trocar o Keycloak como IdP ou emissor de tokens.
