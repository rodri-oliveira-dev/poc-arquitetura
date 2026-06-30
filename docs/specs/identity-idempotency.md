# Especificacao SDD: idempotencia no cadastro de usuarios do IdentityService

## Contexto

O `IdentityService` expoe `POST /api/v1/users` para cadastro de usuarios. O
fluxo atual recebe `username`, `name`, `email`, `password` e `document`, cria o
usuario no Keycloak, gera o `MerchantId` automaticamente, persiste o usuario no
PostgreSQL no schema `identity`, compensa removendo o usuario no Keycloak se a
persistencia local falhar, e dispara domain event depois do commit para envio de
e-mail. A senha nao e persistida localmente.

Esta especificacao registra o comportamento implementado para suporte opcional
ao header `Idempotency-Key` nesse endpoint, incluindo contrato HTTP, persistencia,
concorrencia, retry e limitacoes conhecidas.

## Status de implementacao

Implementado em 2026-06-30.

- `Idempotency-Key` opcional foi exposto em `POST /api/v1/users`.
- A validacao do header ocorre na `IdentityService.Api`.
- A regra de idempotencia, hash logico e decisao de replay ficam na
  `IdentityService.Application`.
- Os registros sao persistidos no schema PostgreSQL `identity`.
- A concorrencia e protegida por constraint unica em
  `(operation_name, idempotency_key)`.
- O contrato OpenAPI versionado documenta o header e os status HTTP esperados.
- Testes unitarios e de integracao cobrem primeira execucao, replay, payload
  diferente, chave invalida e concorrencia.

## Problema

Clientes podem reenviar `POST /api/v1/users` por timeout, falha de rede ou
incerteza sobre o resultado da primeira chamada. Sem idempotencia, um retry pode
tentar criar novo usuario no Keycloak, gerar outro `MerchantId`, persistir outro
registro local ou reenviar e-mail. O cadastro possui efeitos colaterais externos
e locais, entao o retry precisa ser controlado sem transformar a solucao em uma
plataforma generica de idempotencia.

## Objetivos

- Permitir que clientes enviem `Idempotency-Key` opcional em
  `POST /api/v1/users`.
- Garantir que retries com a mesma chave e mesma request logica retornem a
  mesma resposta final, sem repetir Keycloak, MerchantId, persistencia ou e-mail.
- Rejeitar a reutilizacao da mesma chave para request logica diferente.
- Expor comportamento claro para concorrencia enquanto a primeira chamada ainda
  esta em processamento.
- Preservar o fluxo atual quando o header nao for enviado.
- Preservar a compensacao atual quando a persistencia local falhar apos criacao
  no Keycloak.
- Manter a regra de idempotencia na camada `Application`, com persistencia
  concreta na `Infrastructure` e entrada HTTP fina na Minimal API.

## Nao objetivos

- Tornar todos os endpoints do projeto idempotentes.
- Criar uma solucao distribuida generica para qualquer operacao.
- Persistir senha, hash de senha ou segredo equivalente.
- Introduzir Outbox para o `IdentityService`.
- Alterar as regras existentes de cadastro de usuario.
- Alterar autenticacao, autorizacao, scopes ou regra de merchant.
- Garantir retry duravel de envio de e-mail.

## Contrato HTTP

Endpoint afetado:

```http
POST /api/v1/users
Idempotency-Key: <opaque-key>
Content-Type: application/json
Authorization: Bearer <token>
```

O header `Idempotency-Key` e opcional no MVP.

Quando ausente, o comportamento atual deve ser mantido: a request passa pelo
fluxo existente e nao consulta nem grava registro de idempotencia.

Quando presente, o header deve ser validado antes de iniciar o caso de uso:

- tamanho minimo: 1 caractere;
- tamanho maximo: 128 caracteres;
- formato: valor opaco composto apenas por letras, numeros, ponto, underscore,
  dois-pontos e hifen (`^[A-Za-z0-9._:-]{1,128}$`);
- valores com espacos, caracteres de controle ou caracteres nao ASCII devem
  retornar `400 Bad Request` com `ProblemDetails`;
- a chave e case-sensitive;
- o servidor nao deve tentar inferir semantica da chave.

Nome logico da operacao:

```text
CreateUser
```

A combinacao `(operation_name, idempotency_key)` identifica a tentativa
idempotente. Isso evita colisao se outra operacao futura aceitar a mesma chave.

Resposta de sucesso armazenada:

- `response_status_code`: `201`;
- `response_body`: JSON serializado da `CreateUserResponse` atual:
  `id`, `keycloakUserId`, `merchantId`, `username`, `email`;
- header `Location`: `/api/v1/users/{id}` deve ser reproduzido no replay quando
  armazenado ou recalculado a partir do `id` salvo no corpo.

Erros especificos de idempotencia:

- `400 Bad Request`: `Idempotency-Key` invalido.
- `409 Conflict`: chave em processamento.
- `409 Conflict`: chave reutilizada com request logica diferente.

A escolha por `409 Conflict`, em vez de `422 Unprocessable Entity`, e
intencional: a request nova pode ser bem formada e semanticamente valida, mas
conflita com o estado ja reservado para aquela chave idempotente. `422` ficaria
reservado para regras de negocio ou validacoes de dominio que tornam o payload
inaceitavel independentemente da chave.

## Request hash

O `request_hash` deve representar a request logica de cadastro, nao o payload
bruto recebido na rede.

Campos incluidos:

- `operation_name`: `CreateUser`;
- `username`;
- `name`;
- `email`;
- `document`.

Campos excluidos:

- `password`.

A senha nao entra no `request_hash` porque e segredo transitivo enviado apenas
ao Keycloak e nao deve ser persistida, nem mesmo em hash operacional de
idempotencia. Como consequencia explicita, um retry com a mesma
`Idempotency-Key` e mesmos campos logicos, mas senha diferente, deve ser tratado
como a mesma request logica e retornar a resposta salva. O servidor nao deve
reenviar a senha nova ao Keycloak.

Normalizacao proposta antes do hash:

- serializar os campos logicos em JSON canonico com nomes estaveis e ordenados;
- preservar a semantica atual de validacao do endpoint;
- aplicar a mesma normalizacao de valores que o caso de uso ja considerar
  equivalente. Se hoje nao houver trim ou case folding em determinado campo, a
  idempotencia nao deve inventar essa equivalencia.

Formato do hash:

- algoritmo: `SHA-256`;
- armazenamento: string hexadecimal lowercase de 64 caracteres;
- entrada: bytes UTF-8 do JSON canonico.

## Fluxos felizes

### Sem Idempotency-Key

1. Minimal API valida payload como hoje.
2. `CreateUserCommandHandler` executa o cadastro atual.
3. Keycloak e chamado.
4. `MerchantId` e gerado.
5. Usuario e persistido no schema `identity`.
6. Domain event e disparado depois do commit.
7. API retorna `201 Created`.

Nenhum registro de idempotencia e criado.

### Primeira chamada com Idempotency-Key

1. Minimal API captura e valida o header.
2. Application calcula o `request_hash` logico.
3. Application tenta reservar `(CreateUser, Idempotency-Key)` como `Processing`.
4. Se a reserva vencer, o cadastro atual e executado normalmente.
5. Ao concluir com `201 Created`, Application grava a resposta final como
   `Completed`.
6. Retries futuros com a mesma chave e mesmo `request_hash` retornam a resposta
   armazenada.

### Replay com mesma chave e mesma request logica

1. Application encontra registro `Completed`.
2. Compara o `request_hash`.
3. Se o hash for igual, retorna `response_status_code`, `response_body` e
   `Location` equivalentes aos da primeira chamada.
4. Nao chama Keycloak.
5. Nao gera novo `MerchantId`.
6. Nao persiste novo usuario.
7. Nao dispara novo domain event e nao reenvia e-mail.

## Fluxos de erro

### Mesma chave com request logica diferente

1. Application encontra registro existente para `(CreateUser, Idempotency-Key)`.
2. O `request_hash` calculado e diferente do salvo.
3. A API retorna `409 Conflict`.
4. O `ProblemDetails` deve deixar claro que a chave ja foi usada para outra
   request logica.

Exemplo de `title`: `Idempotency key conflict`.

### Mesma chave enquanto a primeira request esta em processamento

1. A primeira request reservou a chave como `Processing`.
2. A segunda request encontra o registro ainda em processamento.
3. Se o `request_hash` for diferente, retorna `409 Conflict` por conflito de
   payload.
4. Se o `request_hash` for igual, retorna `409 Conflict` com `ProblemDetails`
   claro informando que a operacao original ainda esta em processamento.

Exemplo de `title`: `Idempotency key is still processing`.

O MVP nao deve bloquear a segunda request aguardando a primeira terminar. Isso
mantem o comportamento previsivel e evita prender conexoes HTTP.

### Falha antes de qualquer efeito colateral externo

Se a operacao falhar antes de chamar Keycloak, gerar `MerchantId`, persistir
usuario ou disparar domain event, a chave deve permitir retry seguro quando
tecnicamente viavel.

Comportamento preferido:

- remover o registro `Processing` na mesma unidade de trabalho em que a falha
  for detectada; ou
- marcar como `Failed` com `failure_stage = BeforeExternalSideEffect` e permitir
  que a proxima chamada com o mesmo hash tente assumir a chave novamente.

A implementacao atual marca `Failed` com
`failure_stage = BeforeExternalSideEffect` e permite que a proxima chamada com a
mesma chave e mesmo hash reassuma o registro como `Processing`. A spec nao exige
retencao de falhas pre-efeito para auditoria no MVP.

### Falha depois de criar usuario no Keycloak

O comportamento atual de compensacao deve ser preservado:

1. Keycloak cria o usuario.
2. Persistencia local falha antes de concluir o cadastro.
3. Application tenta remover o usuario recem-criado no Keycloak.
4. A excecao original continua sendo propagada conforme comportamento atual.

Registro de idempotencia:

- se a compensacao for concluida, registrar `Failed` com
  `failure_stage = AfterIdentityProviderCompensated`;
- uma nova chamada com a mesma chave e mesmo hash pode ser permitida apos a
  falha, porque o efeito externo foi compensado;
- se a compensacao falhar, registrar `Failed` com
  `failure_stage = AfterIdentityProviderCompensationFailed`;
- nesse caso, retry automatico com a mesma chave nao deve repetir a criacao no
  Keycloak no MVP. Deve retornar `409 Conflict` ou erro operacional claro ate
  haver decisao explicita de recuperacao, para evitar duplicidade ou orfandade.

### Falha depois do commit local

O dispatch de domain events ocorre depois do commit e falhas de handlers nao
revertem o cadastro. Portanto, quando o usuario foi persistido e a resposta
`201` foi produzida, a chave deve ser marcada como `Completed` mesmo que o envio
de e-mail falhe. Retries com a mesma chave retornam a resposta salva e nao
reenviam e-mail.

Mitigacao implementada para a janela entre salvar o usuario e persistir a
resposta idempotente: no caminho com `Idempotency-Key`, o usuario local e a
transicao `Processing -> Completed` sao persistidos no mesmo `SaveChanges` do
EF Core. Se esse commit falhar depois da criacao no Keycloak, a compensacao e
executada e o registro e marcado como `Failed` com o estagio apropriado. O
caminho sem `Idempotency-Key` preserva o fluxo historico.

Se o processo cair depois do commit local concluido e antes da resposta HTTP
chegar ao cliente, o registro ja esta `Completed`; o retry retorna a resposta
persistida e nao reexecuta Keycloak, persistencia local ou e-mail.

## Modelo de dados proposto

Tabela proposta no schema `identity`:

```text
identity.idempotency_records
```

Colunas propostas:

| Coluna | Tipo sugerido | Obrigatorio | Observacao |
| --- | --- | --- | --- |
| `id` | `uuid` | sim | Identificador interno do registro. |
| `operation_name` | `varchar(64)` | sim | Para o MVP: `CreateUser`. |
| `idempotency_key` | `varchar(128)` | sim | Valor opaco recebido no header. |
| `request_hash` | `char(64)` | sim | SHA-256 hex lowercase da request logica. |
| `status` | `varchar(32)` | sim | `Processing`, `Completed`, `Failed`, `Expired`. |
| `failure_stage` | `varchar(64)` | nao | Estagio de falha quando `status = Failed`. |
| `response_status_code` | `integer` | nao | Preenchido para `Completed`. |
| `response_body` | `jsonb` | nao | Corpo final serializado. |
| `response_headers` | `jsonb` | nao | Headers relevantes, inicialmente `Location`. |
| `created_at_utc` | `timestamptz` | sim | Data de criacao. |
| `updated_at_utc` | `timestamptz` | sim | Ultima atualizacao. |
| `expires_at_utc` | `timestamptz` | sim | Limite de reutilizacao da chave. |
| `locked_until_utc` | `timestamptz` | nao | Opcional para recuperar `Processing` abandonado. |
| `correlation_id` | `varchar(128)` | nao | Correlation id da primeira chamada, quando existir. |

Indices e constraints esperados:

- unique index em `(operation_name, idempotency_key)`;
- indice em `expires_at_utc` para limpeza;
- check constraint ou conversao forte para `status`;
- tamanho maximo de `response_body` deve ser avaliado, mas a resposta atual e
  pequena.

Status:

- `Processing`: chave reservada e operacao em andamento.
- `Completed`: operacao concluida com resposta final salva.
- `Failed`: operacao falhou; `failure_stage` define se retry e seguro.
- `Expired`: chave fora do TTL. Pode ser estado materializado ou resultado de
  consulta por `expires_at_utc`.

TTL:

- MVP: 24 horas a partir de `created_at_utc`.
- Apos expiracao, a chave nao precisa garantir replay da resposta antiga.
- A limpeza pode ser job futuro ou rotina administrativa simples; nao e
  requisito implementar limpeza nesta especificacao.

## Decisoes arquiteturais

- A idempotencia pertence ao caso de uso de `Application`, nao a um filtro
  generico da Minimal API.
- A Minimal API deve apenas ler/validar o header e repassar a chave para a
  Application.
- A persistencia dos registros de idempotencia fica na `Infrastructure`, via
  porta da Application.
- O `Domain` nao deve conhecer `Idempotency-Key`, hash, status de processamento,
  HTTP, Keycloak, e-mail ou detalhes de banco.
- O registro `Processing` deve ser criado de forma atomica usando a constraint
  unica do banco para resolver concorrencia entre instancias.
- Nao manter transacao de banco aberta durante chamadas ao Keycloak apenas para
  controlar idempotencia.
- O replay deve reconstruir uma resposta HTTP equivalente a partir do registro
  salvo, sem reexecutar o handler de cadastro.
- Logs devem incluir `operation_name`, status de idempotencia e correlation id,
  mas nunca devem registrar senha, payload completo ou segredo.
- Correlation id da primeira chamada deve ser salvo para auditoria. Em replays,
  o log deve registrar tanto o correlation id atual quanto o original, se
  disponivel.

## Decisoes SDD finais

- `Idempotency-Key` e opcional no MVP.
- Senha nao entra no `request_hash`.
- Retry nao reenviara e-mail quando houver resposta `Completed`.
- `Program.cs` e migrations nao devem ser usados para forcar cobertura.
- Idempotencia deve ficar na `Application`, nao acoplada diretamente a Minimal
  API.
- A persistencia de idempotencia fica em `Infrastructure`, com PostgreSQL como
  mecanismo de coordenacao de concorrencia.
- Operacao ainda em andamento retorna `409 Conflict`; a API nao espera a
  conclusao da chamada original.
- Falha de compensacao no Keycloak bloqueia retry automatico conservadoramente.

## Impacto no OpenAPI

O contrato OpenAPI de `docs/openapi/identity.v1.json` deve permanecer
sincronizado pelo script oficial do repositorio sempre que esse endpoint mudar.

Contrato atual esperado no OpenAPI:

- documentar header opcional `Idempotency-Key` em `POST /api/v1/users`;
- documentar `400 Bad Request` para header invalido, se ainda nao estiver
  representado de forma suficiente;
- manter `201 Created`;
- documentar `409 Conflict` para chave em processamento ou conflito de hash;
- manter `422 Unprocessable Entity` para validacoes existentes de negocio,
  quando aplicavel.
- manter `401 Unauthorized`, `403 Forbidden` e `502 Bad Gateway` quando
  aplicaveis ao contrato existente.

Validacao:

```powershell
dotnet build ./LedgerService.slnx --configuration Release --no-restore
./scripts/contracts/openapi/generate.ps1
npm run openapi:lint
git diff --exit-code -- docs/openapi
```

## Impacto nos testes

Testes implementados:

- unidade do calculo de `request_hash`, garantindo que `password` nao participa;
- unidade do fluxo de replay `Completed`;
- unidade de conflito por mesma chave e hash diferente;
- unidade de concorrencia logica para `Processing`;
- unidade para falha antes de efeito externo com retry permitido;
- unidade para falha apos Keycloak com compensacao bem-sucedida;
- unidade para falha apos Keycloak com compensacao falha;
- integracao com PostgreSQL validando unique constraint por
  `(operation_name, idempotency_key)`;
- integracao do endpoint confirmando primeira execucao `201 Created`;
- integracao do endpoint confirmando que replay nao chama Keycloak novamente;
- integracao do endpoint confirmando que replay nao gera novo `MerchantId`;
- integracao do endpoint confirmando que replay nao persiste novo usuario;
- integracao do endpoint confirmando que replay nao dispara novo e-mail;
- integracao do endpoint confirmando conflito por payload diferente;
- integracao do endpoint confirmando concorrencia com mesma chave;
- unidade de Swagger validando o header opcional.

Nao devem ser adicionados testes artificiais em `Program.cs` ou migrations para
inflar cobertura.

## Criterios de aceite

- Sem `Idempotency-Key`, `POST /api/v1/users` preserva comportamento atual.
- Com primeira `Idempotency-Key`, cadastro retorna `201 Created` e salva
  resposta final.
- Com mesma chave e mesma request logica, replay retorna a mesma resposta sem
  repetir efeitos colaterais.
- Com mesma chave e request logica diferente, retorna `409 Conflict` com
  `ProblemDetails`.
- Com mesma chave ainda em `Processing`, retorna `409 Conflict` com
  `ProblemDetails`.
- Senha nao e persistida, nao entra em hash e nao aparece em logs.
- Falha antes de efeito colateral externo permite retry seguro quando viavel.
- Falha apos criacao no Keycloak preserva compensacao atual e registra o estagio
  de falha.
- A implementacao futura nao altera regras de dominio existentes de cadastro.
- OpenAPI e testes sao atualizados apenas na fase de implementacao.

## Plano de implementacao em fases

### Fase 1: contrato interno e modelo

Status: concluida.

- Criar porta de Application para reserva, leitura e conclusao de registros de
  idempotencia.
- Definir tipos internos para `IdempotencyKey`, `IdempotencyStatus` e
  `IdempotencyOperation`.
- Criar calculador de `request_hash` para `CreateUser`.
- Adicionar entidade/mapping de persistencia em Infrastructure e migration nova.

### Fase 2: integracao no caso de uso

Status: concluida.

- Estender o comando de cadastro para receber chave opcional.
- Aplicar reserva atomica antes de chamar Keycloak.
- Reproduzir resposta quando registro `Completed` for encontrado.
- Registrar `Failed` nos pontos de falha definidos.
- Garantir que replay nao despache domain event nem envie e-mail.

### Fase 3: Minimal API e contrato

Status: concluida.

- Ler e validar o header na Minimal API.
- Mapear erros de idempotencia para `ProblemDetails`.
- Atualizar Swagger/OpenAPI e regenerar `docs/openapi/identity.v1.json`.

### Fase 4: testes e validacao

Status: concluida.

- Adicionar testes unitarios da Application.
- Adicionar testes de persistencia com PostgreSQL.
- Adicionar testes de endpoint para sucesso, replay, conflito e concorrencia.
- Executar build, testes proporcionais e validacao de OpenAPI.

## Riscos e duvidas

- A exclusao de `password` do `request_hash` e uma decisao de seguranca, mas
  significa que uma segunda chamada com mesma chave e senha diferente nao troca
  a senha no Keycloak.
- A implementacao atual usa `locked_until_utc` de 10 minutos para o cadastro de
  usuario. Quando um `Processing` antigo ultrapassa esse limite, o registro e
  marcado como `Failed` com `failure_stage = ProcessingLockExpired` e o retry
  automatico continua bloqueado ate recuperacao operacional. A escolha e
  conservadora para evitar chamar Keycloak duas vezes quando nao ha certeza se o
  efeito externo ocorreu antes da queda.
- Falha de compensacao no Keycloak pode deixar usuario externo orfao; a spec
  evita repetir a criacao no retry, mas recuperacao operacional ainda precisara
  de runbook se o caso se tornar frequente.
- O dispatch atual de domain events nao e duravel. Idempotencia evita reenvio em
  replay, mas nao resolve falha original de envio de e-mail.

## Limitacoes conhecidas

- A idempotencia cobre apenas o cadastro de usuarios em `POST /api/v1/users`.
- O TTL implementado e de 24 horas; limpeza automatica de registros expirados
  ainda e trabalho futuro.
- Chaves em `Processing` com lock expirado sao marcadas como `Failed` com
  `failure_stage = ProcessingLockExpired`, mas retry automatico continua
  bloqueado ate decisao operacional.
- Falha de compensacao no Keycloak pode deixar usuario externo orfao; o retry
  automatico e bloqueado para evitar nova duplicidade.
- Retry HTTP nao reenvia e-mail e tambem nao corrige falha original de envio de
  e-mail pos-commit.
