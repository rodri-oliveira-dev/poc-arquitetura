# Design

## Decisoes

- Criar `BoundedContextCatalog` no projeto `tests/Architecture.Tests`.
- Representar cada contexto com `BoundedContextDescriptor`.
- Representar camadas com `ArchitectureLayer` e providers com
  `MessagingProvider`.
- Declarar dependencias internas permitidas por camada no proprio descritor.
- Declarar referencias compartilhadas permitidas por camada, evitando que
  `KafkaWorkerDefaults` vire permissao global.
- Carregar assemblies do catalogo para ArchUnitNET em vez de manter lista fixa.
- Ler `.csproj` com XML para validar `ProjectReference`, `PackageReference` e
  `FrameworkReference`.
- Usar reflexao para regras de nomes de tipos de dominio onde a politica e
  sobre tipos reais.
- Manter busca textual apenas para politicas lexicais como ausencia de termos
  `PubSub`, `Swagger`, `ControllerBase`, `AddHostedService` e `Stripe` em
  lugares especificos.
- Filtrar comentarios e strings antes das verificacoes lexicais.

## Catalogo inicial

| Contexto | Pasta | Camadas | API | Worker | Persistencia | Providers |
| --- | --- | --- | --- | --- | --- | --- |
| LedgerService | `src/ledger` | Api, Application, Domain, Infrastructure, Worker | sim | sim | sim | Kafka, Pub/Sub |
| BalanceService | `src/balance` | Api, Application, Domain, Infrastructure, Worker | sim | sim | sim | Kafka, Pub/Sub |
| TransferService | `src/transfer` | Api, Application, Domain, Infrastructure, Worker | sim | sim | sim | Kafka |
| PaymentService | `src/payment` | Api, Application, Domain, Infrastructure, Worker | sim | sim | sim | nenhum provider de mensageria |
| IdentityService | `src/identity` | Api, Application, Domain, Infrastructure | sim | nao | sim | nenhum |
| AuditService | `src/audit` | Api, Application, Domain, Infrastructure, Worker | sim | sim | sim | Kafka |

## Dependencias internas padrao

- `Domain`: nenhum projeto interno.
- `Application`: `Domain`.
- `Infrastructure`: `Application` e `Domain`.
- `Api`: `Application` e `Infrastructure`.
- `Worker`: `Application` e `Infrastructure`, quando a camada existir.

## Excecoes deliberadas

- `LedgerService.Worker`, `BalanceService.Worker` e `AuditService.Worker`
  podem referenciar `KafkaWorkerDefaults.csproj`.
- Pub/Sub e permitido somente nos Workers de Ledger e Balance por compatibilidade
  legada explicita.
- Stripe e vocabulario especifico de Payment. A regra nao o proibe dentro de
  Payment, mas impede vazamento para outros bounded contexts.
- Regras lexicais sao usadas somente quando a politica e sobre nomes ou termos
  em codigo e passam por filtro de comentarios/strings.

## Validacao esperada

O fluxo proporcional para esta mudanca e:

```powershell
dotnet tool restore
dotnet restore ./PocArquitetura.slnx
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings --filter "FullyQualifiedName~Architecture.Tests"
```
