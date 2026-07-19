---
name: configuring-opentelemetry-dotnet
description: Use esta skill para revisar, configurar ou evoluir OpenTelemetry em APIs e workers .NET, incluindo traces, métricas, logs, OTLP, correlação, spans customizados e propagação HTTP, Outbox e mensageria. Use os building blocks PetShop.Observability em vez de duplicar helpers. Não use para logging simples ou para adicionar pacotes sem necessidade concreta.
license: MIT
---

# Objetivo

Introduzir observabilidade com propósito claro, baixo acoplamento e cuidado com cardinalidade, custo, dados pessoais e continuidade do contexto distribuído.

A decisão de propagação está registrada em:

```text
docs/adrs/0002-library-propagacao-observabilidade.md
```

A implementação compartilhada fica em:

```text
src/BuildingBlocks/PetShop.Observability/
src/BuildingBlocks/PetShop.Observability.AspNetCore/
```

## Quando usar

- Tracing distribuído.
- Métricas customizadas.
- Exportação OTLP.
- Correlação entre frontend, API, worker, banco e integrações externas.
- `ActivitySource`, `Meter`, tags, baggage e propagação.
- Chamadas HTTP service-to-service.
- Publicação e consumo de mensagens.
- Outbox, retry, DLQ e replay.
- Diagnóstico de latência, erro, throughput ou backlog.

## Regras obrigatórias de propagação

- Use `PetShop.Observability` em APIs, workers, jobs e adapters; não copie helpers W3C para cada serviço.
- Use `PetShop.Observability.AspNetCore` apenas em executáveis web.
- Mantenha `CorrelationId` independente de `TraceId`.
- Em HTTP de saída, use `CorrelationIdDelegatingHandler` para `X-Correlation-Id`.
- Não injete manualmente `traceparent`, `tracestate` ou `baggage` em `HttpClient`; use a instrumentação OpenTelemetry padrão.
- Não envie `tenant_id` como header HTTP de autoridade.
- Em mensagens tenant-owned, propague `correlation_id`, `tenant_id`, `traceparent`, `tracestate` e `baggage`.
- Converta headers nativos do broker para `Dictionary<string, string>` no adapter de Infrastructure.
- Consumers devem extrair o snapshot, iniciar Activity `Consumer` e abrir o contexto de execução antes do processamento.
- Publishers devem iniciar Activity `Producer`, capturar o contexto desse span e injetá-lo nos headers.
- Retry, DLQ e replay preservam todos os headers canônicos.

## Outbox

Ao introduzir Outbox:

1. capture o contexto durante o caso de uso original;
2. persista `correlation_id`, `tenant_id`, `traceparent`, `tracestate` e `baggage`;
3. no relay, restaure o snapshot como parent da Activity `Producer`;
4. capture novamente o contexto depois de iniciar a Activity;
5. publique os headers do novo span.

Não capture apenas o contexto do polling da Outbox, pois isso quebra a relação com a operação de origem.

## Processo

1. Leia `AGENTS.md`, ADR-0002, configuração e instrumentação existentes.
2. Identifique o objetivo:
   - latência;
   - erro;
   - volume;
   - concorrência;
   - dependência externa;
   - processamento agendado;
   - conflito ou cancelamento;
   - continuidade entre transportes.
3. Escolha entre log, trace, métrica ou combinação.
4. Verifique pacotes existentes antes de adicionar dependência.
5. Reutilize os building blocks antes de criar abstração nova.
6. Mantenha nomes de `ActivitySource`, `Meter`, operações e métricas estáveis.
7. Use tags de baixa cardinalidade.
8. Adicione spans customizados somente para etapas arquiteturalmente relevantes.
9. Atualize documentação e ADR quando o contrato de observabilidade mudar.
10. Valide build, testes e exportação local quando disponível.

## Configuração por executável

Cada API ou worker deve definir explicitamente:

- `service.name` único e estável;
- `service.version` quando disponível;
- instrumentações realmente utilizadas;
- `ActivitySource` próprios;
- exporter e endpoint OTLP por configuração;
- sampling coerente com ambiente e custo.

A library de propagação não escolhe vendor APM, collector, sampling ou dashboards.

## Sugestões para o domínio

Métricas úteis podem incluir:

- agendamentos criados, cancelados e reagendados;
- conflitos de disponibilidade;
- tempo para confirmar um agendamento;
- atendimentos por estado;
- duração real versus prevista;
- falhas de notificação;
- reservas temporárias expiradas;
- backlog de Outbox e idade da mensagem mais antiga;
- mensagens processadas, duplicadas, reenviadas e enviadas à DLQ.

Evite usar como labels:

- identificador de tenant;
- correlation ID;
- identificador de tutor;
- identificador de pet;
- appointment ID;
- e-mail;
- telefone;
- mensagem completa de erro;
- event ID ou message ID únicos.

Esses identificadores podem aparecer em logs ou traces quando necessários e protegidos, mas não como dimensões de métricas.

## Baggage

- Use somente contexto pequeno, necessário e não sensível.
- Não transporte token, senha, e-mail, telefone, nome de tutor, nome de pet ou payload.
- Evite duplicar dados já presentes no contrato da mensagem.
- Considere limites do broker e o custo de propagar baggage em toda chamada.

## Segurança

- Não registre tokens, senhas, dados pessoais ou payloads completos.
- Headers recebidos não substituem autenticação ou autorização.
- O `tenant_id` da mensagem delimita contexto de processamento, mas o consumer ainda deve validar ownership.
- Não exponha stack trace ao cliente.
- Não habilite exporter de console como padrão de produção.
- Não crie health check pesado que consulte grande volume de agenda, Outbox ou DLQ.

## Checklist de revisão

- O executável registrou os `ActivitySource` usados?
- A chamada HTTP utiliza a instrumentação padrão e o handler de correlation?
- O publisher injeta o contexto da Activity `Producer`, não apenas o parent antigo?
- O consumer usa `ActivityKind.Consumer` e o parent recebido?
- Outbox persiste o snapshot original?
- Retry, DLQ e replay preservam os headers?
- Mensagens tenant-owned carregam `tenant_id`?
- Baggage está livre de PII e segredos?
- Tags e métricas evitam alta cardinalidade?
- Testes comprovam continuidade de trace e correlation?

## Restrições

- Não instalar instrumentação automática sem biblioteca correspondente em uso.
- Não criar uma métrica para cada evento sem pergunta operacional clara.
- Não usar tracing como substituto de logs ou métricas.
- Não trocar de APM ou vendor sem decisão explícita.
- Não adicionar dependência de Kafka, Pub/Sub ou outro broker aos building blocks.
- Não criar variações locais dos nomes de headers canônicos.

## Saída esperada

- objetivo observado;
- instrumentação adicionada ou corrigida;
- como o contexto é propagado entre HTTP, Outbox e mensageria;
- riscos de cardinalidade e privacidade;
- testes e validações executadas;
- limitações conhecidas.
