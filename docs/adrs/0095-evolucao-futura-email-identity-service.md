# ADR-0095: Evolucao futura do envio de e-mails do IdentityService

## Status
Proposto

## Data
2026-06-26

## Contexto
O envio atual de e-mail do `IdentityService` e um side effect intra-processo
executado depois do commit local por Domain Event Dispatcher. Esse desenho e
suficiente para a etapa atual da POC, mas nao oferece entrega duravel, retry
persistente, DLQ ou reprocessamento operacional.

Se o envio de e-mail passar a ser requisito critico, a arquitetura deve evoluir
sem acoplar Application ou Domain a Resend, SMTP, filas ou workers especificos.

## Decisao
Registrar a direcao futura para evoluir o envio de e-mails do `IdentityService`
com Outbox Pattern, mensageria, retry, Dead Letter Queue e worker dedicado.

Quando essa evolucao for necessaria, o desenho alvo sera:

- persistir o evento ou comando de e-mail em uma Outbox transacional no schema
  `identity`;
- publicar mensagens por provider de mensageria escolhido explicitamente em ADR
  propria;
- processar envio de e-mails em worker dedicado;
- aplicar retry controlado com backoff e limite de tentativas;
- mover falhas definitivas para DLQ com payload, tipo, correlation id, causa e
  timestamp;
- permitir reprocessamento operacional seguro;
- manter `IEmailSender` como porta para envio concreto;
- manter Resend e Mailpit encapsulados na Infrastructure ou em adapters do
  worker.

Esta ADR nao escolhe Kafka, Pub/Sub ou outro transporte para identidade. Essa
decisao deve ser tomada apenas quando houver necessidade concreta de mensageria
para o `IdentityService`.

## Consequencias

### Beneficios esperados
- Entrega de e-mail passa a sobreviver a restart do processo.
- Falhas temporarias podem ser reprocessadas com controle.
- Falhas definitivas ficam diagnosticaveis por DLQ.
- O request HTTP de cadastro permanece desacoplado do envio real.
- A operacao ganha visibilidade sobre pendencias e erros de e-mail.

### Custos e limitacoes
- Aumenta complexidade operacional com tabelas de Outbox, worker, retry e DLQ.
- Exige novos testes de integracao, observabilidade e runbook.
- Exige ADR especifica para provider de mensageria e contratos de mensagem.
- Nao deve ser implementado enquanto o envio de e-mail for apenas side effect
  simples da POC.

### Gatilhos para implementar
- Necessidade de garantia duravel de envio.
- Necessidade de reprocessamento operacional.
- Falhas de provider externo com impacto relevante no fluxo de negocio.
- Volume ou latencia que justifique separar envio em worker.
- Necessidade de auditoria de notificacoes.

## Fora do escopo
- Implementar Outbox nesta etapa.
- Definir topicos, subscriptions, schemas de mensagem ou contratos JSON.
- Criar worker de e-mail agora.
- Alterar o fluxo atual de cadastro de usuarios.
