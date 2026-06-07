# Runbook de recuperacao de eventos

Este runbook consolida a operacao de DLQ, retry, replay, reconstrucao de
projecao e relatorio de divergencia no projeto. Ele serve como guia de estudo
arquitetural e como checklist para revisao tecnica.

Pub/Sub e o provider principal. Kafka permanece como provider legado opcional.
Este documento nao cria endpoint, script, workflow ou comportamento novo. Para
detalhes e contexto, use:

- [Estrategia operacional de DLQ](dlq-strategy.md)
- [Estrategia operacional de replay seguro](replay-strategy.md)
- [Rebuild de projecao do Balance](projection-rebuild.md)
- [Diagnostico de replay, DLQ e projecao](../reports/replay-dlq-projection-diagnostics.md)

## Decisao rapida

| Situacao | Acao inicial |
| --- | --- |
| Falha transitoria de banco, broker, timeout, credencial corrigivel ou lock expirado. | Retry pelo mecanismo do ponto do fluxo. |
| Mensagem em DLQ com payload invalido, versao nao suportada ou contrato quebrado. | Manter evidencia e avaliar descarte auditado. |
| Mensagem em DLQ valida, causa raiz corrigida e idempotencia garantida. | Avaliar replay ou redrive controlado. |
| Evento ja aplicado no Balance pelo mesmo `payload.id`. | Tratar como duplicidade esperada. |
| Saldo materializado diverge de eventos validos da fonte historica. | Gerar relatorio de divergencia e planejar rebuild parcial. |
| Regra historica mudou ou projecao precisa ser recalculada. | Nao usar DLQ como fonte primaria. Planejar reconstrucao de projecao. |

## Quando investigar DLQ

Investigue DLQ quando houver:

- alerta de DLQ de aplicacao maior que zero ou crescimento sustentado;
- falha ao publicar na DLQ;
- queda de consumo sem queda de publicacao;
- aumento anormal de `nack`, retry ou mensagens sem commit;
- divergencia entre eventos publicados e saldos projetados;
- mensagem relevante para merchant, periodo ou evento financeiro critico;
- necessidade de decidir descarte, retry, replay, redrive ou rebuild.

Antes de agir, diferencie:

- Outbox `DeadLetter`: falha antes da entrega ao broker, tratada por requeue da
  Outbox.
- DLQ de aplicacao: mensagem rejeitada pelo consumidor depois da entrega.
- DLQ tecnica Pub/Sub: falha de entrega do transporte no Pub/Sub real.
- Rebuild de projecao: recomputacao planejada a partir de fonte historica
  confiavel.

## Classificacao de falhas

| Classe | Sinais | Decisao comum |
| --- | --- | --- |
| Transitoria | Timeout, banco indisponivel, broker instavel, throttling, lock expirado. | Retry, sem confirmar a mensagem original enquanto a falha persistir. |
| Permanente | Consumidor errado, configuracao incompativel, regra funcional que sempre falha. | Corrigir causa raiz e avaliar descarte ou replay aprovado. |
| Contrato invalido | JSON invalido, schema invalido, campos ausentes, campos extras rejeitados. | DLQ e descarte auditado, salvo decisao formal de evento corretivo. |
| Versao nao suportada | `event_type`, `eventName` ou `eventVersion` ausente, divergente ou desconhecido. | Nao reprocessar ate existir suporte ou decisao de compatibilidade. |
| Duplicidade | Mesmo `payload.id` ja registrado em `processed_events`. | Sucesso idempotente, sem replay para alterar saldo. |
| Divergencia de projecao | Eventos validos diferem da leitura atual de `daily_balances`. | Relatorio de divergencia e rebuild planejado. |

## Quando fazer retry

Use retry quando a falha for recuperavel e repetir a mesma mensagem puder
produzir sucesso sem editar payload:

- Pub/Sub: usar `nack` ou deixar a redelivery ocorrer conforme a subscription.
- Kafka: nao commitar offset original ate o processamento ser seguro.
- Outbox: manter backoff por `NextRetryAt` ou requeue administrativo quando a
  mensagem estiver em `DeadLetter` e a causa de publicacao tiver sido corrigida.

Nao use retry para poison message, contrato invalido, versao desconhecida,
mensagem operacional enviada ao consumidor errado ou regra permanente.

## Quando fazer replay ou redrive

Use replay ou redrive quando todos os pontos forem verdadeiros:

- a mensagem ou conjunto de eventos foi localizado e rastreado;
- o contrato e valido;
- a versao e suportada pelo consumidor alvo;
- a idempotencia esta garantida;
- a causa raiz foi corrigida;
- o impacto foi estimado;
- a decisao foi registrada.

Use redrive quando a origem for uma DLQ e a acao for recolocar uma mensagem
valida no fluxo principal. Use replay quando a acao for reprocessar uma
mensagem, filtro ou conjunto de fatos de forma intencional e auditada.

## Quando descartar mensagem

Descarte somente com registro operacional quando:

- o payload e invalido e nao ha decisao formal de correcao;
- `eventName`, `eventVersion` ou `event_type` nao sao recuperaveis de fonte
  confiavel;
- a versao nao e suportada;
- a mensagem ja foi aplicada de forma idempotente e nao requer acao;
- a mensagem viola regra de negocio permanente;
- a DLQ contem evidencia de envio para consumidor errado;
- o replay exigiria editar payload manualmente.

O descarte nao deve apagar a evidencia antes de registrar motivo, origem,
metadados, horario, operador ou automacao e resultado observado.

## Quando reconstruir projecao

Reconstrua a projecao quando o problema estiver na leitura materializada, nao
na entrega individual:

- saldo atual diverge dos eventos validos da fonte historica;
- regra de projecao precisa ser reaplicada para uma janela controlada;
- eventos ja aplicados com o mesmo `payload.id` nao podem ser corrigidos por
  replay idempotente;
- ha necessidade de comparar saldo atual e saldo reconstruido antes de alterar
  dados.

Nao use DLQ como fonte primaria de rebuild. A DLQ contem falhas, duplicidades e
evidencia parcial. A fonte atual recomendada para os casos internos e a Outbox
historica do Ledger, conforme [rebuild de projecao](projection-rebuild.md).

## Quando gerar relatorio de divergencia

Gere relatorio de divergencia antes de qualquer reconstrucao quando:

- houver suspeita de saldo incorreto;
- o escopo for `merchantId`, periodo e versao de evento;
- for preciso decidir se um rebuild parcial e necessario;
- houver eventos invalidos, duplicados ou fora do filtro que possam afetar o
  resultado;
- a equipe precisar de evidencia tecnica para revisao.

O relatorio atual e calculado sem persistencia, nao altera `daily_balances`,
nao altera `processed_events` e retorna `Mutated=false`.

## Fluxo Pub/Sub

1. Localizar a mensagem na subscription principal, na subscription de inspecao
   da DLQ de aplicacao ou na DLQ tecnica nativa do Pub/Sub real.
2. Identificar attributes, incluindo `event_type`, `event_id`,
   `correlation_id`, `traceparent`, `tracestate`, `baggage`, `dlq_reason`,
   `original_source`, `original_provider` e `original_metadata_*` quando
   existirem.
3. Validar `eventName` derivando de `event_type`. Exemplo:
   `LedgerEntryCreated.v2` vira `LedgerEntryCreated`.
4. Validar `eventVersion` derivando o ultimo segmento de `event_type`.
   Exemplo: `LedgerEntryCreated.v2` vira `v2`.
5. Validar payload. Se a mensagem estiver em envelope `DeadLetterMessage`,
   extrair o payload original antes da validacao.
6. Confirmar se o consumidor alvo suporta o evento e se a idempotencia por
   `payload.id` permanece preservada.
7. Decidir acao:
   - `nack` para falha transitoria;
   - `ack` para sucesso, duplicidade idempotente ou DLQ publicada com sucesso;
   - descarte auditado para payload invalido ou versao nao suportada;
   - replay ou redrive controlado para mensagem valida com causa corrigida;
   - relatorio de divergencia ou rebuild para problema de projecao.
8. Registrar resultado com message id, topic, subscription, ordering key quando
   houver, attributes relevantes, decisao, motivo, operador ou automacao,
   horario, logs, traces e metricas.

## Fluxo Kafka

1. Localizar a mensagem por topic, partition e offset, ou pelos metadados
   `original_topic`, `original_partition` e `original_offset` quando estiver em
   DLQ.
2. Identificar headers, incluindo `event_type`, `event_id`, `correlation_id`,
   `traceparent`, `tracestate`, `baggage`, `dlq_reason`, `original_source` e
   `original_provider`.
3. Identificar message key. Preserve a key original quando ela representar
   particionamento ou ordenacao funcional.
4. Validar `eventName` derivando de `event_type`.
5. Validar `eventVersion` derivando o ultimo segmento de `event_type`.
6. Validar payload. Se o value estiver em envelope `DeadLetterMessage`, extrair
   o payload original antes da validacao.
7. Confirmar se o consumidor alvo suporta o evento e se a idempotencia por
   `payload.id` permanece preservada.
8. Decidir acao:
   - nao commitar offset para falha transitoria;
   - commitar como sucesso idempotente quando o evento ja foi aplicado;
   - publicar em DLQ e commitar a original apenas depois da DLQ bem sucedida;
   - descartar de forma auditada para payload invalido ou versao nao suportada;
   - replay ou redrive controlado para mensagem valida com causa corrigida;
   - relatorio de divergencia ou rebuild para problema de projecao.
9. Registrar resultado com topic, partition, offset, message key, headers
   relevantes, decisao, motivo, operador ou automacao, horario, logs, traces e
   metricas.

## Checklist antes de replay

- Contrato valido.
- Idempotencia garantida.
- Versao suportada.
- Motivo documentado.
- Dry-run executado, se aplicavel.
- Impacto conhecido.
- Causa raiz corrigida.
- Escopo, filtros e limite definidos.
- `payload.id`, correlacao e tracing preservados.
- Resultado esperado definido antes da execucao.

## Checklist antes de reconstrucao

- Escopo definido.
- Fonte de eventos definida.
- Dry-run ou relatorio de divergencia executado.
- Eventos invalidos avaliados.
- Duplicidades avaliadas.
- Resultado validado.
- `merchantId`, periodo fechado e `eventVersion` definidos.
- Eventos fora do filtro avaliados.
- Estrategia de registro operacional definida.
- Criterio de parada e rollback operacional conhecidos.

## Comandos e referencias existentes

Nao ha endpoint administrativo publico para replay, replay por filtro,
rebuild parcial ou relatorio de divergencia. A execucao deve ocorrer por uma
superficie operacional interna que resolva `ISender` no composition root do
`BalanceService.Worker` ou ferramenta controlada equivalente.

Casos de uso internos documentados:

- `ManualEventReplayCommand`: replay manual de mensagem unica.
- `FilteredEventReplayCommand`: replay por filtro simples com dry-run padrao.
- `PartialProjectionRebuildCommand`: reconstrucao parcial da projecao.
- `ProjectionRebuildDivergenceReportCommand`: relatorio de divergencia sem
  mutacao.

Requeue administrativo de Outbox disponivel no Ledger:

- `GET /api/v1/outbox/dead-letters`
- `POST /api/v1/outbox/dead-letters/{id}/requeue`

Referencias detalhadas:

- [Replay manual e replay por filtro](replay-strategy.md#implementacao-disponivel)
- [Replay para reconstrucao de projecao](replay-strategy.md#replay-para-reconstrucao-de-projecao)
- [Relatorio de divergencia](projection-rebuild.md#caso-de-uso-interno)
- [Requeue de Outbox](../development/kafka-outbox.md#dlq-em-banco-e-requeue-operacional)
- [Operacao do Pub/Sub](pubsub.md)
- [Contratos logicos de eventos](../events/README.md)
- [Diagnostico operacional](../reports/replay-dlq-projection-diagnostics.md)

## Registro operacional minimo

Registre sempre:

- solicitante e motivo;
- provider, origem e tipo de DLQ;
- message id Pub/Sub ou topic, partition e offset Kafka;
- Outbox message id quando a origem for Outbox;
- `eventName`, `eventVersion` e `event_type`;
- `payload.id` usado para idempotencia;
- correlation id e trace id quando existirem;
- classificacao da falha;
- decisao tomada;
- resultado do dry-run, replay, redrive, descarte, requeue, relatorio ou
  rebuild;
- links para logs, traces, metricas e evidencias.

## Limitacoes conhecidas

- Nao ha redrive versionado implementado para DLQ de aplicacao Pub/Sub ou
  Kafka.
- Pub/Sub local nao simula DLQ tecnica nativa.
- Kafka e provider legado opcional.
- Nao ha endpoint publico para replay, replay por filtro, rebuild parcial ou
  relatorio de divergencia.
- A fonte concreta atual para replay por filtro e rebuild usa
  `ledger.outbox_messages`; leitura direta de broker ou DLQ exige adapter
  operacional futuro.
- `accountId` nao e filtro seguro para rebuild atual, pois os contratos
  `LedgerEntryCreated.v1` e `LedgerEntryCreated.v2` nao possuem esse campo e
  `daily_balances` agrega por merchant, data e moeda.
- O relatorio de divergencia nao persiste auditoria dedicada.
- Rebuild parcial nao troca tabela, nao trunca a projecao inteira e nao corrige
  payload invalido.

## Proximos passos recomendados

1. Implementar redrive versionado de DLQ Pub/Sub e Kafka com validacao de
   schema, dry-run, limites e auditoria persistente.
2. Definir superficie operacional segura para os casos de uso internos de
   replay, replay por filtro, rebuild e relatorio de divergencia.
3. Persistir decisoes operacionais de descarte, redrive e replay.
4. Padronizar nomenclatura entre `outbox_message_id`, id tecnico de transporte
   e `payload.id`.
5. Avaliar consumer Pub/Sub para `ReprocessamentoLancamentosSolicitado.v1` ou
   outro mecanismo alinhado ao provider principal.
6. Evoluir rebuild com auditoria persistente, criterios de rollback e estrategia
   explicita para volumes maiores.
7. Criar adapters operacionais para ler DLQ Pub/Sub e Kafka como fontes de
   replay por filtro, preservando payload original e metadados.
