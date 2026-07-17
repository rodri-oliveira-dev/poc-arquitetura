# ADR-0111: Consistencia em cancelamentos no cadastro do IdentityService

## Status
Aceito

## Data
2026-07-16

## Contexto
O cadastro de usuarios do `IdentityService` cria o usuario no Keycloak e depois
persiste o vinculo local no PostgreSQL. A [ADR-0096](./0096-idempotencia-cadastro-usuarios-identity-service.md)
ja definiu idempotencia opcional, replay seguro e compensacao em falhas de
persistencia.

A revisao posterior identificou uma janela especifica: quando a requisicao era
cancelada depois da criacao do usuario externo e antes da confirmacao local, os
`catch` filtrados por `!cancellationToken.IsCancellationRequested` podiam impedir
a compensacao e deixar o registro idempotente em `Processing`.

## Decisao
Refinar o cadastro para controlar explicitamente os estagios da operacao:
usuario externo nao criado, usuario criado no Keycloak, persistencia local
iniciada e persistencia local confirmada.

Quando o usuario externo ja tiver sido criado e a persistencia local ainda nao
tiver sido confirmada, o handler tenta remover o usuario no Keycloak mesmo que a
excecao original seja `OperationCanceledException`. A compensacao usa token
proprio com timeout configuravel em
`IdentityService:CreateUserConsistency:CompensationTimeout`, sem depender do
token HTTP cancelado.

Se o cancelamento ocorrer dentro do client administrativo apos o Keycloak criar
o usuario e antes da senha ser definida, o proprio client executa a remocao com
token proprio limitado por `IdentityProvider:Keycloak:CompensationTimeout`.

Depois da confirmacao local, o handler nao compensa o Keycloak. O envio de
e-mail segue como side effect best effort pos-commit e nao entra no limite de
consistencia distribuida.

A idempotencia tambem passa a classificar cancelamentos apos reserva como falha,
salvando o estado de recuperacao com token independente. Falhas antes do efeito
externo ou apos compensacao confirmada permanecem reexecutaveis; falhas de
compensacao continuam bloqueando retry automatico.

## Consequencias
- Reduz a janela conhecida de usuario orfao no Keycloak por cancelamento HTTP.
- Evita compensacao depois de commit local confirmado.
- Impede que cancelamento da requisicao seja usado como unico token para
  recuperacao operacional.
- Mantem a solucao localizada no caso de uso, sem Saga generica, Outbox ou
  Worker.
- Mantem senha fora de logs, hash e persistencia local.

## Limites
- Commits ambiguos em que o PostgreSQL confirma, mas o cliente perde a
  confirmacao, ainda exigem diagnostico operacional.
- Falha da propria compensacao do Keycloak pode deixar usuario externo sem
  vinculo local; retry automatico permanece bloqueado nesse caso.

## Atualizacao em 2026-07-17
Uma revisao posterior fechou a janela restante entre a criacao confirmada no
Keycloak e o inicio do bloco compensavel. A regiao compensavel agora comeca logo
apos o `KeycloakUserId` ser registrado no estado de execucao e inclui geracao de
`MerchantId`, construcao de value objects, criacao do agregado, persistencia
local e confirmacao de commit. A decisao continua sem Saga, Outbox ou Worker
novo para este fluxo.
