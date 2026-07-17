# Design

## Diagnostico confirmado

O `CreateUserCommandHandler` chamava `identityProvider.CreateUserAsync` e
marcava `CreateUserExecutionState.MarkIdentityProviderUserCreated`. Em seguida,
fora do `try/catch`, executava geracao do merchant, construcao dos value objects
e criacao do agregado local. O `try/catch` compensavel comecava apenas antes de
`users.AddAsync`.

Essa ordem era equivalente a:

```text
Criar usuario no Keycloak
Marcar efeito externo como concluido
Gerar MerchantId
Construir value objects
Construir agregado User
Entrar no try/catch
Persistir no PostgreSQL
```

## Decisao tecnica

Manter a solucao localizada no caso de uso. Depois que o `KeycloakUserId` e
conhecido e registrado no estado de execucao, o handler entra imediatamente na
regiao compensavel. Dentro dela ficam:

- geracao do `MerchantId`;
- construcao de `MerchantId`, `Email` e `Username`;
- chamada a `User.Register`;
- `AddAsync`;
- `SaveChangesAsync` quando o fluxo nao esta delegado ao servico de
  idempotencia;
- marcacao de persistencia local confirmada.

A compensacao continua centralizada em `CompensateIdentityProviderAsync`, que:

- nao faz nada quando o efeito externo nao ocorreu;
- nao faz nada depois de `LocalPersistenceConfirmed`;
- usa `CancellationTokenSource` proprio com timeout configuravel;
- registra falha de compensacao sem expor payload sensivel;
- retorna `failure_stage` usado pelo servico de idempotencia.

No fluxo com `Idempotency-Key`, o handler continua executando sem
`SaveChangesAsync` proprio. O `IdempotencyService` marca a resposta como
`Completed` em memoria e faz um commit unico do usuario local e do registro
idempotente. Se esse commit falhar, o callback de persistencia usa o mesmo
estado de execucao para compensar e classificar a falha.

## Estado necessario

Nao foi criada uma maquina de estados generica. O estado minimo continua sendo:

- efeito externo nao iniciado: `KeycloakUserId` ausente;
- usuario externo confirmado: `KeycloakUserId` presente;
- persistencia local iniciada: `LocalPersistenceStarted`;
- persistencia local confirmada: `LocalPersistenceConfirmed`;
- compensacao confirmada ou falha: representada pelo `failure_stage`
  idempotente retornado pela compensacao.

## Plano tecnico curto

1. Mover geracao/modelagem/criacao do agregado para dentro da regiao
   compensavel do handler.
2. Adicionar testes unitarios para falhas antes de `AddAsync`.
3. Adicionar testes de cancelamento em `AddAsync`, timeout de compensacao e
   retry idempotente apos compensacao.
4. Atualizar documentacao SDD e docs afetadas.
5. Executar validacoes proporcionais no IdentityService.

## Riscos residuais

Commits ambiguos em que o PostgreSQL confirma, mas o processo perde a
confirmacao antes de marcar `LocalPersistenceConfirmed`, continuam exigindo
analise operacional. A implementacao evita compensar depois de confirmacao
local conhecida, mas nao transforma commit ambiguo em uma transacao distribuida
forte.
