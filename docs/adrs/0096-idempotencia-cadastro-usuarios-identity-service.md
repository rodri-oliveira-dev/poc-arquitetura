# ADR-0096: Idempotencia no cadastro de usuarios do IdentityService

## Status
Aceito

## Data
2026-06-30

## Contexto
O `IdentityService` expoe `POST /api/v1/users` para cadastrar usuarios no
Keycloak, gerar `MerchantId`, persistir o vinculo local no PostgreSQL e disparar
o e-mail de boas-vindas por Domain Event depois do commit local.

Clientes podem repetir a chamada por timeout, queda de rede ou incerteza sobre
o resultado da primeira requisicao. Sem idempotencia, um retry poderia tentar
criar outro usuario no Keycloak, gerar outro `MerchantId`, persistir novo
registro local ou reenviar e-mail.

A decisao precisava preservar o fluxo de cadastro ja definido na
[ADR-0090](./0090-cadastro-usuarios-identity-service.md), manter Keycloak como
dono das credenciais e evitar persistir senha, hash de senha ou segredo
equivalente.

## Decisao
Adicionar suporte opcional ao header `Idempotency-Key` em
`POST /api/v1/users`.

Quando o header nao e enviado, o endpoint preserva o fluxo historico e nao cria
registro de idempotencia. Quando o header e enviado, a `Api` valida o formato e
repassa a chave para a `Application`.

A `Application` calcula um hash da request logica de cadastro e executa a
operacao por um servico de idempotencia. O hash inclui `operation_name`,
`username`, `name`, `email` e `document`; a senha nao entra no hash. A senha
continua sendo enviada apenas ao Keycloak e nao e persistida pelo
`IdentityService`.

A `Infrastructure` persiste registros em `identity.idempotency_records` no
PostgreSQL. A combinacao `(operation_name, idempotency_key)` e unica e resolve
concorrencia entre instancias pela constraint do banco. O registro usa status
`Processing`, `Completed` e `Failed`, com `failure_stage` para distinguir
falhas antes de efeito externo, falhas compensadas e falhas de compensacao.

Retries com a mesma chave e mesmo payload logico retornam a resposta `201`
armazenada, sem chamar Keycloak novamente, sem gerar outro `MerchantId`, sem
persistir outro usuario e sem reenviar e-mail. A reutilizacao da mesma chave com
payload logico diferente retorna `409 Conflict`. Uma chamada concorrente
enquanto a primeira ainda esta em processamento tambem retorna `409 Conflict`.

Falhas antes de efeitos externos ou depois de compensacao bem-sucedida no
Keycloak permitem nova tentativa com a mesma chave e mesmo hash. Falhas em que a
compensacao do Keycloak nao foi confirmada permanecem bloqueadas para evitar
duplicidade externa.

## Consequencias positivas
- Retries de cadastro tornam-se seguros para clientes que usam
  `Idempotency-Key`.
- A resposta final pode ser recuperada apos timeout do cliente sem repetir
  efeitos colaterais.
- Concorrencia e resolvida por constraint unica no PostgreSQL, sem manter
  transacao aberta durante chamada ao Keycloak.
- A senha permanece fora do banco local e fora do hash operacional de
  idempotencia.
- O envio de e-mail nao e repetido por replay idempotente.
- A regra fica na `Application`, mantendo `Domain` sem dependencia de HTTP,
  Keycloak, e-mail ou detalhes de banco.

## Trade-offs
- O cadastro com `Idempotency-Key` depende de uma tabela adicional e de limpeza
  futura por TTL; no MVP, a retencao e limitada por `expires_at_utc`, mas a
  limpeza operacional ainda nao foi automatizada.
- Uma segunda chamada com mesma chave e senha diferente, mas mesmo payload
  logico, e tratada como replay. Isso e intencional para nao persistir nem
  comparar senha, mas significa que retry nao altera senha no Keycloak.
- Operacoes em `Processing` retornam `409 Conflict` em vez de aguardar a primeira
  chamada terminar. O comportamento e previsivel, mas exige retry posterior do
  cliente.
- Falha de compensacao no Keycloak exige intervencao ou decisao operacional
  futura antes de liberar novo retry seguro.

## Alternativas consideradas
1. **Nao implementar idempotencia no cadastro**
   - Rejeitada porque retries de clientes poderiam duplicar efeitos externos e
     locais.

2. **Idempotencia generica em middleware HTTP**
   - Rejeitada porque o cadastro envolve Keycloak, persistencia local,
     compensacao e e-mail. A regra precisa conhecer a operacao logica, nao
     apenas o payload HTTP bruto.

3. **Incluir `password` no hash da request**
   - Rejeitada porque a senha e segredo transitivo. Mesmo hash operacional de
     senha aumentaria superficie de risco e contrariaria a decisao de manter o
     Keycloak como unico dono das credenciais.

4. **Bloquear a segunda request ate a primeira terminar**
   - Rejeitada para evitar conexoes HTTP presas e acoplamento temporal entre
     chamadas concorrentes. O cliente deve receber `409 Conflict` e tentar de
     novo depois.

5. **Usar Outbox para e-mail nesta etapa**
   - Rejeitada para manter o escopo pequeno. A evolucao duravel do envio de
     e-mails continua registrada na
     [ADR-0095](./0095-evolucao-futura-email-identity-service.md).

## Relacao com Keycloak, PostgreSQL, e-mail e compensacao
- **Keycloak** continua sendo o provider principal de identidade e o unico dono
  das credenciais. Replay idempotente nao chama a Admin API novamente.
- **PostgreSQL** armazena o vinculo local do usuario e os registros de
  idempotencia no schema `identity`; a constraint unica protege concorrencia.
- **E-mail** permanece side effect pos-commit via Domain Event. Replay de uma
  resposta `Completed` nao dispara novo Domain Event e nao reenvia e-mail.
- **Compensacao** continua removendo o usuario recem-criado no Keycloak quando a
  modelagem ou persistencia local falha apos a criacao externa e antes da
  confirmacao local. O `failure_stage` registra se a falha ocorreu antes de
  efeito externo, depois de compensacao concluida ou depois de compensacao nao
  confirmada.

## Fora do escopo
- Reset, troca ou reenvio de senha.
- Reenvio manual de e-mail.
- Desativacao ou exclusao de usuario.
- Outbox, DLQ ou worker dedicado para eventos de identidade.
- Tornar todos os endpoints do projeto idempotentes.
