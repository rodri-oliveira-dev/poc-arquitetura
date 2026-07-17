# Design

## Diagnostico confirmado

O fluxo sem `Idempotency-Key` criava usuario no Keycloak, adicionava usuario no
repositorio local e chamava `SaveChangesAsync`. Em falhas nao canceladas, o
handler compensava removendo o usuario externo. Em `OperationCanceledException`
com o token da requisicao cancelado, o filtro do `catch` impedia compensacao.

No fluxo com `Idempotency-Key`, `IdempotencyService` reservava `Processing`,
executava o cadastro sem salvar imediatamente, marcava a resposta como
`Completed` e fazia um unico `SaveChangesAsync` para usuario local e registro
idempotente. Quando esse `SaveChangesAsync` era cancelado, a excecao tambem
escapava sem marcar o registro como `Failed`; o registro podia permanecer
`Processing` ate expirar o lock.

## Decisoes

- O handler passa a manter um estado explicito da execucao do cadastro:
  `NotStarted`, `IdentityProviderUserCreated`, `LocalPersistenceStarted` e
  `LocalPersistenceConfirmed`.
- A compensacao e acionada para qualquer excecao depois da criacao do usuario
  externo, inclusive `OperationCanceledException`, enquanto
  `LocalPersistenceConfirmed` ainda for falso.
- A compensacao usa `CancellationTokenSource` proprio com timeout configuravel
  em `IdentityService:CreateUserConsistency:CompensationTimeout`.
- Quando o cancelamento ocorre dentro do `KeycloakAdminClient` depois do
  `POST /users` e antes do reset de senha, o client tambem compensa com token
  proprio limitado por `IdentityProvider:Keycloak:CompensationTimeout`.
- A idempotencia passa a persistir falha tambem em cancelamentos apos reserva,
  usando `CancellationToken.None` para a gravacao de recuperacao e para o
  callback de compensacao.
- A persistencia local confirmada continua sendo o ponto sem retorno. Depois
  disso, o cadastro local vence; e-mail segue best effort e replay idempotente
  nao reenvia e-mail.
- Falha de salvar o proprio registro de falha nao mascara a excecao original.
  O estado em memoria fica classificado e, se a gravacao falhar, o retry futuro
  cai no mecanismo de lock/TTL de `Processing`.

## Recuperacao

- `BeforeExternalSideEffect`: retry automatico e seguro.
- `AfterIdentityProviderCompensated`: retry automatico e seguro.
- `AfterIdentityProviderCompensationFailed`: retry automatico bloqueado; requer
  investigacao operacional para evitar duplicidade ou orfandade.
- `ProcessingLockExpired`: retry automatico bloqueado conservadoramente ate
  recuperacao operacional.

## Logs e dados sensiveis

Os logs continuam usando hash curto da chave idempotente e identificador do
usuario Keycloak apenas quando necessario para compensacao. Senha, token,
payload completo e `Idempotency-Key` bruto nao devem ser registrados.

## Limites conhecidos

Se o processo ou a conexao falhar em uma janela ambigua em que o PostgreSQL
tenha confirmado o commit, mas o cliente EF nao tenha recebido confirmacao, o
sistema nao tem uma prova local confiavel do commit. A implementacao reduz a
janela conhecida de cancelamento da requisicao, mas recuperacao de commits
ambiguos continua sendo assunto operacional.
