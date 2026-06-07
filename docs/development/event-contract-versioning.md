# Politica de versionamento de contratos de eventos

Esta politica orienta futuras mudancas nos contratos de eventos entre `LedgerService` e `BalanceService`. Ela vale para Pub/Sub, que e o provider principal, e para Kafka, que permanece como provider legado opcional.

O objetivo e permitir evolucao incremental sem quebrar consumidores antigos, sem misturar semantica de negocio com detalhes de transporte e sem criar divergencia entre providers.

## Fontes de verdade

- Contratos logicos atuais: [`../events/README.md`](../events/README.md).
- JSON Schemas versionados: [`../../contracts/events/README.md`](../../contracts/events/README.md).
- Decisao de `LedgerEntryCreated.v2`: [`../adrs/0084-ledger-entry-created-v2-currency-explicita.md`](../adrs/0084-ledger-entry-created-v2-currency-explicita.md).
- Mensageria, Outbox e DLQ: [`kafka-outbox.md`](kafka-outbox.md).
- Contrato Pub/Sub entre infraestrutura e aplicacao: [`pubsub-infra-app-contract.md`](pubsub-infra-app-contract.md).

## Contrato logico

Contrato logico de evento e a representacao versionada do fato ou da solicitacao de negocio que produtor e consumidor concordam em processar. Ele inclui nome do evento, versao, payload, semantica dos campos, obrigatoriedade, formatos, regras de idempotencia e exemplos validos.

O contrato logico nao e o envelope fisico do provider. O mesmo contrato deve ser publicado por Pub/Sub e Kafka com o mesmo JSON de payload. Isso evita que o `BalanceService` precise entender regras de negocio diferentes dependendo do transporte usado, preserva os testes de contrato e reduz risco durante migracoes entre providers.

## Transporte

Transporte e a forma como o provider entrega o evento. Ele inclui topic fisico, subscription, attributes, headers, ordering key, message key, ack, nack, commit, retry, DLQ, offset, partition e metadados operacionais.

Pub/Sub e Kafka podem ter metadados diferentes, mas nao podem mudar o significado do evento. Uma mensagem `LedgerEntryCreated.v2` deve representar o mesmo fato financeiro nos dois providers.

## O que pertence ao payload

O payload deve conter somente dados necessarios para o consumidor entender e aplicar a regra de negocio do evento.

Pertence ao payload:

- identificador logico do fato ou da solicitacao, como `id`;
- campos de negocio, como `amount`, `currency`, `merchantId`, `occurredAt` e `type`;
- valores necessarios para idempotencia de negocio, quando a regra depender deles;
- correlacao logica ja assumida pelo contrato, como `correlationId`, quando documentada no payload;
- campos opcionais de negocio, como `description` e `externalReference`.

Nao pertence ao payload:

- offset, partition, delivery attempt, ack deadline ou subscription;
- topic fisico, consumer group ou nome de fila;
- headers ou attributes especificos do provider;
- motivo de DLQ ou metadados de diagnostico;
- campos que existem apenas para rotear uma mensagem em um provider.

## O que pertence a Pub/Sub attributes

Attributes do Pub/Sub carregam metadados tecnicos e de roteamento. Elas nao fazem parte do JSON Schema do payload.

Attributes obrigatorias para eventos versionados:

- `event_id`: identificador tecnico da publicacao, atualmente relacionado ao Outbox.
- `event_type`: nome e versao do contrato, por exemplo `LedgerEntryCreated.v2`.

Attributes opcionais ou condicionais:

- `correlation_id`: correlacao tecnica quando disponivel fora do payload.
- `traceparent`, `tracestate` e `baggage`: propagacao W3C e baggage.
- `dlq_reason`, `original_source`, `original_provider` e `original_metadata_*`: somente em mensagens de DLQ de aplicacao.

## O que pertence a Kafka headers

Headers do Kafka carregam metadados tecnicos equivalentes aos attributes do Pub/Sub. Eles nao fazem parte do JSON Schema do payload.

Headers obrigatorios para eventos versionados:

- `event_id`: identificador tecnico da publicacao, atualmente relacionado ao Outbox.
- `event_type`: nome e versao do contrato, por exemplo `LedgerEntryCreated.v2`.

Headers opcionais ou condicionais:

- `correlation_id`: correlacao tecnica quando disponivel fora do payload.
- `traceparent`, `tracestate` e `baggage`: propagacao W3C e baggage.
- `dlq_reason`, `original_topic`, `original_partition` e `original_offset`: somente em mensagens de DLQ de aplicacao.

## Mudancas compativeis

Uma mudanca compativel preserva a capacidade de consumidores existentes processarem mensagens novas sem erro e sem mudar o resultado de negocio esperado.

Sao compativeis:

- adicionar campo opcional ao payload, desde que consumidores antigos ignorem campos desconhecidos ou tenham sido atualizados antes;
- adicionar novo valor tolerado pelo consumidor, quando o valor nao mudar a semantica de valores existentes;
- adicionar metadata tecnica opcional em Pub/Sub attributes ou Kafka headers;
- adicionar novo attribute ou header que nao seja exigido por consumidores antigos;
- melhorar descricao, exemplo ou documentacao sem mudar schema ou comportamento;
- ampliar validacao do produtor sem rejeitar payloads validos do contrato atual;
- fazer consumidor ignorar campos desconhecidos quando isso estiver explicitamente previsto para a familia do evento.

Mesmo mudancas compativeis devem atualizar documentacao, schema quando aplicavel, exemplos e testes de contrato.

## Mudancas breaking

Uma mudanca breaking exige nova versao do contrato ou rollout coordenado que preserve compatibilidade ate todos os consumidores estarem prontos.

Sao breaking:

- remover campo obrigatorio;
- mudar tipo de campo, como string decimal para number;
- mudar formato documentado, como date-time para date;
- mudar significado semantico de um campo existente;
- renomear campo;
- adicionar campo obrigatorio sem nova versao;
- tornar obrigatorio um campo antes opcional;
- alterar `event_type` ou event name de forma incompativel;
- mudar regra de idempotencia, como trocar `payload.id` por outro identificador;
- alterar sinal, escala ou interpretacao de valores financeiros;
- exigir novo header ou attribute para processar evento antigo;
- alterar versionamento sem manter leitura da versao anterior durante a convivencia;
- mover dado de negocio do payload para transporte;
- fazer Pub/Sub e Kafka publicarem payloads logicamente diferentes para o mesmo `event_type`.

## Quando criar nova versao

Crie `v2`, `v3` ou versao posterior quando:

- a semantica de negocio mudar;
- um novo campo obrigatorio for necessario;
- consumidores antigos nao conseguirem processar corretamente o payload novo;
- a regra de idempotencia mudar;
- houver mudanca em valores financeiros, moeda, agregacao, temporalidade ou ownership por merchant;
- o nome do evento ou o significado do evento precisar mudar;
- a evolucao exigir rejeitar mensagens que eram validas na versao anterior.

A nova versao deve manter o nome base quando representar a mesma familia de evento, por exemplo `LedgerEntryCreated.v2`. Use novo nome de evento apenas quando o fato de negocio for outro.

## Politica para v1 legado

Versoes `v1` legadas podem conviver com versoes novas enquanto houver backlog, producers antigos, Kafka legado ou necessidade operacional documentada.

Regras de convivencia:

- o consumidor pode aceitar `v1` e versoes novas no mesmo topic fisico da familia de evento;
- fallback de negocio so e permitido quando estiver documentado no contrato legado e em ADR aplicavel;
- `LedgerEntryCreated.v1` permite fallback para `BRL` somente durante normalizacao de mensagens legadas;
- fallback nao deve ser aplicado silenciosamente na versao nova quando o campo for obrigatorio;
- novos producers devem emitir a versao atual documentada;
- remocao de leitura de `v1` exige evidencia de que nao ha backlog, producer legado ativo nem replay esperado;
- remocao deve atualizar contrato, documentacao, schemas, testes e, se relevante, ADR.

Tempo minimo de convivencia:

- manter a leitura da versao anterior por pelo menos um ciclo operacional completo do ambiente afetado;
- em ambiente real, manter tambem pelo periodo de retencao configurado para o transporte e para qualquer mecanismo de replay;
- se nao houver metrica confiavel de backlog e producer ativo, nao remover a versao legada.

## Regras para Pub/Sub

Payload:

- `PubsubMessage.Data` deve conter o JSON do payload logico.
- O JSON Schema valida somente o payload logico.

Attributes obrigatorias:

- `event_id`;
- `event_type`.

Attributes recomendadas:

- `correlation_id`;
- `traceparent`;
- `tracestate`;
- `baggage`.

Ordering key:

- deve derivar do agregado documentado quando `EnableMessageOrdering=true`;
- deve permanecer vazia quando ordering estiver desabilitado;
- nao deve carregar dado de negocio que falte no payload.

Retry, ack e nack:

- `Ack` somente apos sucesso de processamento ou apos publicacao bem sucedida na DLQ de aplicacao;
- `Nack` em falha recuperavel nao classificada pelo processor ou cancelamento;
- retry nativo deve respeitar a subscription e nao substituir idempotencia do consumidor;
- mensagens invalidas por contrato devem ser classificadas e enviadas para DLQ de aplicacao.

DLQ:

- DLQ de aplicacao deve preservar payload original e metadados de diagnostico;
- DLQ tecnica nativa pode existir em GCP real, mas nao muda o contrato logico;
- motivo de DLQ pertence a attributes da mensagem de DLQ, nao ao payload original.

## Regras para Kafka

Payload:

- `Message.Value` deve conter o JSON do payload logico.
- O JSON Schema valida somente o payload logico.

Headers obrigatorios:

- `event_id`;
- `event_type`.

Headers recomendados:

- `correlation_id`;
- `traceparent`;
- `tracestate`;
- `baggage`.

Message key:

- deve derivar do agregado documentado;
- influencia particionamento e ordenacao dentro da partition;
- nao deve ser a unica fonte de um dado de negocio necessario ao consumidor.

Retry, commit e DLQ:

- commit manual deve ocorrer somente apos sucesso de processamento ou apos publicacao bem sucedida na DLQ de aplicacao;
- falhas recuperaveis podem bloquear commit para permitir reprocessamento conforme politica do worker;
- mensagens invalidas por contrato devem ser classificadas e enviadas para DLQ de aplicacao;
- DLQ deve preservar payload original e headers relevantes;
- offsets, partitions e motivo de DLQ pertencem a headers ou envelope de DLQ, nao ao payload original.

## Schemas

Cada versao de evento deve ter JSON Schema proprio em [`../../contracts/events`](../../contracts/events). O schema representa apenas o payload logico compartilhado por Pub/Sub e Kafka.

Regras:

- usar nome em kebab-case com versao, por exemplo `ledger-entry-created.v2.schema.json`;
- manter exemplos validos e invalidos em [`../../contracts/events/examples`](../../contracts/events/examples);
- nao validar headers, attributes, topic, key ou DLQ no schema do payload;
- alinhar campos obrigatorios com a documentacao do evento;
- atualizar testes de produtor e consumidor quando o schema mudar;
- nao alterar schema de versao antiga para aceitar semantica nova.

## Validacao automatizada

Os JSON Schemas e exemplos versionados em [`../../contracts/events`](../../contracts/events) sao validados por ferramenta Node versionada no projeto. O script usa Ajv com suporte aos formatos `date`, `date-time` e `uuid`.

Execute localmente:

```bash
npm ci
npm run events:validate
```

A validacao confere:

- todos os arquivos `.schema.json` sao JSON validos e compilam como JSON Schema;
- cada schema possui exemplo `<evento>.valid.json` correspondente em `contracts/events/examples`;
- exemplos `*.valid.json` passam no schema correspondente;
- exemplos `*.invalid.json` falham no schema correspondente, quando existirem;
- exemplos sem schema correspondente falham com mensagem explicita.

O workflow `event-contracts` roda esta validacao em `pull_request` para `main`, `push` em `main` e `workflow_dispatch`, restrito aos caminhos relacionados a contratos de eventos, exemplos, documentacao e tooling Node.

## Testes

Mudancas em contrato devem ter testes proporcionais no produtor e no consumidor.

Produtor:

- valida que o payload publicado atende ao schema da versao correta;
- valida `event_type` em Pub/Sub attributes ou Kafka headers;
- valida que o payload nao depende de metadados de transporte;
- valida exemplos quando houver utilitario local para isso.

Consumidor:

- aceita payload valido da versao suportada;
- rejeita payload invalido e encaminha para DLQ quando aplicavel;
- preserva idempotencia documentada;
- aceita versoes legadas ainda suportadas;
- testa fallback legado somente quando a politica do evento permitir.

Testes devem cobrir Pub/Sub e Kafka quando a mudanca afetar adapters, headers, attributes, ack, nack, commit, retry ou DLQ. Build completo nao e obrigatorio para mudancas puramente documentais.

## Depreciacao

Depreciar uma versao nao significa remove-la imediatamente.

Fluxo recomendado:

1. Documentar a versao nova e marcar a antiga como legado.
2. Atualizar producer para emitir a versao nova.
3. Manter consumer lendo a versao antiga e a nova.
4. Medir ou inspecionar backlog, DLQ, replay esperado e producer legado.
5. Comunicar a janela de remocao no documento do evento.
6. Remover suporte antigo somente quando nao houver mensagens pendentes nem producer ativo.
7. Atualizar docs, schemas aplicaveis, testes e ADR quando a remocao mudar uma decisao arquitetural relevante.

## Checklist para novos eventos

Antes de publicar um novo evento, confirme:

- documento em `docs/events/<evento>-vN.md`;
- entrada no indice [`../events/README.md`](../events/README.md);
- JSON Schema em [`../../contracts/events`](../../contracts/events);
- exemplo valido em [`../../contracts/events/examples`](../../contracts/events/examples);
- exemplo invalido quando ajudar a documentar uma regra de rejeicao;
- teste de contrato do produtor;
- teste de contrato do consumidor;
- definicao de `event_type`;
- definicao de idempotencia;
- definicao de Pub/Sub attributes obrigatorias;
- definicao de Kafka headers obrigatorios;
- definicao de ordering key ou justificativa para nao usar;
- definicao de message key;
- comportamento de retry, ack, nack, commit e DLQ;
- atualizacao de README ou indice relevante;
- ADR quando houver decisao arquitetural, contrato entre servicos, estrategia de mensageria, persistencia, observabilidade, seguranca ou mudanca relevante de comportamento.
