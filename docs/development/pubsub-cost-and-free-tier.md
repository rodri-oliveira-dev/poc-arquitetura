# Custo e free tier do Pub/Sub

Este guia registra os fatores de custo que devem ser revisados antes de usar o
Pub/Sub real na GCP. Ele nao substitui a [pagina oficial de precos do
Pub/Sub](https://cloud.google.com/pubsub/pricing), que deve ser consultada antes
de estimar ou aprovar um ambiente.

## Free tier do Pub/Sub standard

No Pub/Sub standard, os primeiros `10 GiB` mensais de throughput classificados
no SKU basico de entrega de mensagens sao gratuitos por billing account.
Publicacao e entrega contam no consumo desse limite: publicar uma mensagem e
entrega-la para uma subscription sao operacoes cobradas separadamente. Cada nova
subscription que recebe a mensagem aumenta o throughput de entrega.

O calculo considera no minimo `1 KB` por request, independentemente do tamanho
das mensagens incluídas. Para mensagens pequenas, batch pode reduzir o
throughput faturavel. O tamanho real tambem inclui attributes e metadados
definidos pelo Pub/Sub; a formula abaixo e uma aproximacao inicial.

## Provavelmente dentro do free tier

Uma POC de baixa volumetria provavelmente permanece dentro do free tier quando
usa:

- mensagens pequenas, preferencialmente publicadas em batch quando aplicavel;
- Pub/Sub standard;
- baixa quantidade de mensagens;
- `retain_acked_messages=false`;
- nenhum snapshot;
- nenhuma BigQuery subscription;
- nenhuma Cloud Storage subscription;
- nenhum import topic;
- nenhum Single Message Transform (SMT);
- nenhum Pub/Sub Lite.

Essa classificacao nao e garantia de custo zero. O volume agregado da billing
account, a quantidade de subscriptions, redeliveries, DLQs, backlog e trafego
entre regioes precisam ser medidos.

## Pode gerar custo adicional

Os seguintes itens podem sair do free tier ou gerar SKUs adicionais:

- throughput total acima do free tier mensal;
- backlog acumulado;
- DLQ de aplicacao ou DLQ tecnica acumulada;
- mensagens nao reconhecidas retidas por mais de `24 horas`;
- retencao customizada no topic;
- `retain_acked_messages=true`;
- snapshots;
- BigQuery subscriptions;
- Cloud Storage subscriptions;
- import topics;
- Single Message Transforms (SMTs);
- AI Inference SMT, caso esteja disponivel e seja habilitado;
- trafego entre regioes;
- Pub/Sub Lite.

BigQuery subscriptions, Cloud Storage subscriptions e import topics possuem
regras proprias de preco; o free tier basico de `10 GiB` nao se aplica ao
throughput desses recursos. UDF SMTs tambem possuem cobranca adicional pelos
dados processados.

Backlog nao reconhecido com mais de `24 horas` pode gerar armazenamento
cobravel. Retencao configurada no topic, mensagens reconhecidas retidas e
snapshots tambem podem gerar armazenamento cobravel. Nesta POC, as subscriptions
retêm mensagens nao reconhecidas por ate sete dias por padrao; portanto, DLQs
sem triagem merecem monitoramento mesmo em baixa volumetria.

O Pub/Sub Lite foi descontinuado e seu desligamento entrou em vigor em
`18 de marco de 2026`. Ele nao deve ser adotado por esta POC. A comunicacao
oficial sobre SMTs publicou AI Inference SMT como evolucao planejada; confirme
disponibilidade e preco atual antes de considerar esse recurso.

## Formula inicial de estimativa

Use a aproximacao abaixo para iniciar a estimativa mensal:

```text
publish_throughput = quantidade_de_mensagens_publicadas * tamanho_medio_da_mensagem
subscribe_throughput = quantidade_de_mensagens_entregues * tamanho_medio_da_mensagem
throughput_total_estimado = publish_throughput + subscribe_throughput + redeliveries + DLQ
```

`quantidade_de_mensagens_entregues` deve considerar todas as subscriptions.
`redeliveries` e `DLQ` representam throughput adicional aproximado, nao somente
contagem de mensagens. Para uma estimativa mais fiel, aplique o minimo faturavel
de `1 KB` por request e considere batching, attributes e metadados.

## Dados necessarios para uma estimativa real

Colete pelo menos:

- tamanho medio da mensagem, incluindo attributes relevantes;
- mensagens publicadas por mes;
- numero de subscriptions que recebem cada mensagem;
- taxa de redelivery;
- taxa de DLQ de aplicacao e DLQ tecnica;
- tempo medio de backlog;
- regioes de publisher e subscriber.

Tambem registre batching efetivo, retencao configurada e recursos adicionais
habilitados, como subscriptions de exportacao, import topics ou SMTs.

## Referencias oficiais

- [Precos do Pub/Sub](https://cloud.google.com/pubsub/pricing)
- [Visao geral de Pub/Sub Single Message Transforms](https://cloud.google.com/pubsub/docs/smts/smts-overview)
- [Anuncio de Single Message Transforms](https://cloud.google.com/blog/products/data-analytics/pub-sub-single-message-transforms)
