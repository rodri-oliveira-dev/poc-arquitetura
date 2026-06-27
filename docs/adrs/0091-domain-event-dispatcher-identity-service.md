# ADR-0091: Domain Event Dispatcher no IdentityService

## Status
Aceito

## Data
2026-06-26

## Contexto
O `IdentityService.Domain` modela `User` como aggregate root. O cadastro de
usuario gera efeitos secundarios, como envio de e-mail de boas-vindas, que nao
devem ficar acoplados ao endpoint HTTP nem ao handler principal de cadastro.

Ao mesmo tempo, a POC ainda nao implementa Outbox para eventos de identidade.
O dispatch inicial precisa ser simples, local ao processo e coerente com a
fronteira entre Domain, Application e Infrastructure.

## Decisao
Adotar Domain Events no `IdentityService` com dispatch depois do commit do EF
Core.

O padrao decidido e:

- entidades de dominio acumulam eventos por meio da base `Entity`;
- aggregate roots, como `User`, publicam eventos de dominio ao executar
  operacoes relevantes;
- `User.Register` adiciona `UserRegisteredDomainEvent`;
- `IdentityDbContext` coleta domain events antes de salvar;
- `IdentityDbContext` executa `base.SaveChanges` ou `base.SaveChangesAsync`;
- somente depois do commit local bem-sucedido, o `IDomainEventDispatcher`
  despacha os eventos coletados;
- handlers implementam `IDomainEventHandler<TEvent>`;
- side effects ficam em handlers registrados pela Infrastructure.

O dispatcher atual e intra-processo e executa handlers registrados no container
de DI. Falhas de handlers sao registradas em log e nao revertem o commit ja
concluido.

## Consequencias

### Beneficios
- Mantem o aggregate root como fonte do fato de dominio.
- Evita side effects diretamente no endpoint HTTP.
- Separa persistencia do usuario e reacoes ao cadastro.
- Permite adicionar novos handlers para o mesmo domain event sem alterar o caso
  de uso principal.
- Preserva `Domain` sem dependencia de e-mail, SMTP, Resend, Keycloak Admin API
  ou EF Core.

### Custos e limitacoes
- Como o dispatch ocorre depois do commit e sem Outbox, side effects podem
  falhar apos o usuario estar persistido.
- O dispatcher atual nao oferece retry duravel, DLQ ou reprocessamento.
- Handlers precisam ser idempotentes ou tolerantes a repeticao caso o padrao
  evolua para Outbox no futuro.
- Side effects nao devem ser usados para manter invariantes obrigatorias do
  aggregate, pois rodam depois da persistencia.

### Impactos operacionais
- Falhas em handlers devem ser diagnosticadas por logs.
- O envio de e-mail de boas-vindas e uma reacao ao
  `UserRegisteredDomainEvent`, nao parte da transacao local de cadastro.
- A evolucao para Outbox deve preservar o contrato logico dos domain events.

## Fora do escopo
- Publicar domain events em Kafka ou Pub/Sub.
- Garantir entrega duravel de side effects.
- Criar DLQ para eventos de identidade nesta etapa.
- Transformar o dispatcher em mecanismo de integracao entre bounded contexts.
