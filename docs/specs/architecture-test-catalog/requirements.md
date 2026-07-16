# Catalogo declarativo de bounded contexts para testes arquiteturais

## Contexto

Os testes arquiteturais estavam concentrados em listas fixas para
`LedgerService`, `BalanceService`, `TransferService` e `PaymentService`.
`IdentityService` e `AuditService` ja existem na solution agregadora, mas nao
eram cobertos pelo conjunto principal de regras de camadas, providers e
referencias internas.

## Descoberta da arquitetura real

- `LedgerService`: `Api`, `Application`, `Domain`, `Infrastructure` e
  `Worker`; persistencia EF Core; Kafka como provider padrao e Pub/Sub legado
  explicito no Worker.
- `BalanceService`: `Api`, `Application`, `Domain`, `Infrastructure` e
  `Worker`; persistencia EF Core; Kafka como provider padrao e Pub/Sub legado
  explicito no Worker.
- `TransferService`: `Api`, `Application`, `Domain`, `Infrastructure` e
  `Worker`; persistencia EF Core; Kafka-only conforme ADR-0087.
- `PaymentService`: `Api`, `Application`, `Domain`, `Infrastructure` e
  `Worker`; persistencia EF Core; integra Stripe por vocabulario e adapters do
  proprio contexto, sem mensageria financeira propria.
- `IdentityService`: `Api`, `Application`, `Domain` e `Infrastructure`;
  persistencia EF Core; nao possui Worker.
- `AuditService`: `Api`, `Application`, `Domain`, `Infrastructure` e
  `Worker`; persistencia EF Core; Worker Kafka para `AuditRecordRequested.v1`.

`src/Shared` e uma area compartilhada tecnica, nao um bounded context. A pasta
`src/Auth.Api` contem apenas artefatos de build sem projeto fonte catalogavel.

## Requisitos verificaveis

- Catalogar `LedgerService`, `BalanceService`, `TransferService`,
  `PaymentService`, `IdentityService` e `AuditService`.
- Declarar pasta fisica, assemblies, camadas existentes, API, Worker,
  persistencia, providers permitidos, dependencias internas permitidas e regras
  especificas.
- Suportar contextos sem Worker.
- Suportar contextos com API e Worker.
- Detectar diretorios de contexto em `src` que tenham projetos `*Service.*` e
  nao estejam catalogados.
- Falhar quando um novo contexto for adicionado sem governanca arquitetural.
- Validar que `Domain` nao depende de ASP.NET, EF Core, Kafka, Pub/Sub ou
  Stripe SDK.
- Validar que `Application` nao depende de HTTP, Swagger, EF Core, Kafka ou
  Pub/Sub.
- Validar que `Api` nao depende de componentes de Worker.
- Validar que `Worker` nao depende de apresentacao HTTP.
- Validar referencias de projetos entre camadas com base no catalogo.
- Preservar excecoes deliberadas como politica explicita.
- Manter regras de Kafka, Pub/Sub e Stripe somente nos contextos aplicaveis.
- Reduzir fragilidade de regras textuais, usando assemblies, `.csproj`,
  reflexao e ArchUnitNET quando a regra nao for lexical.
- Ignorar comentarios e strings nas poucas regras lexicais restantes.

## Criterios de aceitacao

- Todos os bounded contexts atuais participam da suite arquitetural.
- Um novo contexto nao catalogado falha em teste deterministico.
- A suite diferencia Identity sem Worker dos demais contextos com Worker.
- Pub/Sub permanece limitado aos Workers de Ledger e Balance.
- Kafka permanece limitado aos Workers dos contextos que o catalogo permite.
- Stripe permanece vocabulario especifico de Payment.
- Testes do mecanismo cobrem catalogo atual, contexto desconhecido, contexto
  sem Worker, contexto com Worker, dependencia proibida, dependencia permitida,
  provider especifico e ausencia de falso positivo textual.

## Fora do escopo

- Mover arquivos de producao.
- Uniformizar artificialmente os servicos.
- Exigir MediatR em todos os contextos.
- Criar Shared Domain ou novo shared kernel.
- Alterar contratos HTTP, eventos ou comportamento de negocio.
- Fazer push, merge ou release.
