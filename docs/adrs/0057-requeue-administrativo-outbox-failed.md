# ADR-0057: Requeue administrativo de mensagens Outbox Failed

## Status
Aceito

## Data
2026-05-08

## Contexto

O `OutboxPublisherService` publica mensagens pendentes do Ledger pela porta `IOutboxMessagePublisher`, implementada atualmente pelo adapter Kafka. Quando uma mensagem excede `Outbox:Publisher:MaxAttempts`, ela passa para `Failed` e deixa de ser reclamada pelo publisher.

Sem fluxo operacional de recuperacao, um lancamento pode ficar persistido corretamente no Ledger, mas o evento financeiro correspondente pode nao chegar ao Balance. Isso quebra a consistencia eventual esperada para a projecao de saldo.

## Decisao

Adicionar um endpoint administrativo protegido em `LedgerService.Api` para recolocar mensagens Outbox `Failed` em `Pending`.

O endpoint `POST /api/v1/outbox/failed/requeue` exige o scope especifico `ledger.outbox.requeue`, delega a regra para um comando de Application e altera apenas mensagens com status `Failed`. Mensagens `Sent`, `Pending` ou `Processing` nao sao reprocessadas por esse fluxo.

O comando exige motivo operacional e ao menos um filtro (`outboxMessageId`, `eventType`, `occurredFrom` ou `occurredUntil`). O limite maximo por chamada e 100 mensagens para reduzir risco de republicacao ampla sem investigacao.

O requeue registra auditoria persistente na propria mensagem Outbox:

- contador de requeues;
- data/hora do ultimo requeue;
- operador extraido do token;
- motivo informado.

## Consequencias

- Operadores podem recuperar mensagens `Failed` depois de corrigir falhas transientes de Kafka, configuracao ou infraestrutura.
- O publisher existente continua responsavel pela publicacao, preservando retry, backoff, locks e headers atuais.
- A API passa a expor uma superficie administrativa nova e protegida por scope dedicado.
- O schema de `outbox_messages` passa a manter metadados do ultimo requeue.

## Beneficios

- Fecha o risco operacional de mensagens financeiras ficarem permanentemente em `Failed` sem caminho suportado de recuperacao.
- Evita reprocessar mensagens ja publicadas com sucesso.
- Mantem rastreabilidade minima sem introduzir uma tabela de auditoria completa para a POC.
- Preserva idempotencia downstream: republicacoes de `LedgerEntryCreated.v1` continuam carregando o mesmo evento financeiro, e o Balance usa `processed_events`.

## Trade-offs / custos

- A auditoria persistente guarda o ultimo requeue e o contador, nao o historico completo de todas as intervencoes.
- O endpoint administrativo depende de governanca de tokens/scopes no ambiente.
- Requeue nao corrige falhas permanentes de contrato ou topico; nesses casos a causa raiz deve ser corrigida antes da chamada.
- Reprocessar muitos eventos pode gerar carga no publisher, Kafka e consumidores.

## Alternativas consideradas

1. **Worker periodico de reconciliacao automatica**
   - Rejeitado porque poderia mascarar falhas permanentes de contrato, configuracao ou topico e republicar indefinidamente sem decisao humana.

2. **Script SQL documentado sem endpoint**
   - Rejeitado como solucao principal porque espalharia regra operacional fora da aplicacao, com maior risco de atualizar estados indevidos.

3. **Reprocessar tambem mensagens `Processing` expiradas pelo endpoint**
   - Rejeitado porque o publisher ja trata `Processing` com lock expirado no fluxo normal de claim; o endpoint administrativo deve operar apenas em falhas finais.

4. **Tabela de auditoria completa de Outbox**
   - Adiada para evolucao futura. Para a POC, contador e metadados do ultimo requeue entregam rastreabilidade proporcional ao risco.
