# ADR-0002: Library compartilhada para propagação de observabilidade

- **Status:** Aceita
- **Data:** 2026-07-18
- **Decisão:** padronizar correlação e contexto W3C em building blocks agnósticos de mensageira

## Contexto

A plataforma começa como monólito modular, mas deverá permitir a extração futura de serviços e a adoção de mensageria sem perder continuidade de trace, correlação operacional ou isolamento multitenant.

A POC anterior demonstrou que propagar apenas `correlation_id` não mantém um trace distribuído contínuo. Para preservar o fluxo HTTP, Outbox, broker e consumer, também é necessário transportar e restaurar:

- `traceparent`;
- `tracestate`;
- `baggage`.

Processos assíncronos não possuem acesso ao token HTTP original. Por isso, mensagens e jobs tenant-owned também precisam transportar `tenant_id` explicitamente.

Sem uma implementação compartilhada, cada serviço ou adapter de broker poderia criar nomes de headers, regras de parsing, tratamento de baggage e hierarquia de spans diferentes.

## Decisão

Serão criados dois assemblies:

### `PetShop.Observability`

Núcleo agnóstico de ASP.NET, vendor APM e mensageira, responsável por:

- nomes canônicos dos headers de propagação;
- snapshot serializável do contexto atual;
- captura de `correlation_id`, `tenant_id`, `traceparent`, `tracestate` e `baggage`;
- injeção e extração em `IDictionary<string, string>`;
- contexto ambiente baseado em `AsyncLocal` com escopos restauráveis;
- `DelegatingHandler` para `X-Correlation-Id` em HTTP de saída;
- criação de Activities `Producer` e `Consumer`;
- registro das abstrações em DI.

### `PetShop.Observability.AspNetCore`

Adapter web responsável por:

- validar ou criar `X-Correlation-Id` como GUID;
- disponibilizar o mesmo valor no request e response;
- ler `tenant_id` do principal autenticado;
- enriquecer Activity e logging scope;
- abrir o contexto de execução usado pelos demais handlers.

A separação evita que workers dependam do shared framework ASP.NET Core.

## Headers canônicos

| Header | Uso |
| --- | --- |
| `X-Correlation-Id` | Correlação HTTP operacional |
| `correlation_id` | Correlação em mensagens e jobs |
| `tenant_id` | Ownership multitenant fora do ciclo HTTP |
| `traceparent` | Parent W3C do trace |
| `tracestate` | Estado adicional W3C |
| `baggage` | Contexto propagável não sensível |

`CorrelationId` permanece independente de `TraceId`.

## HTTP

A library adicionará apenas `X-Correlation-Id` por `DelegatingHandler`.

A propagação de `traceparent`, `tracestate` e `baggage` em chamadas HTTP deve usar a instrumentação padrão do `HttpClient` do OpenTelemetry. Não será criada uma segunda implementação manual concorrente.

`tenant_id` não será enviado como header HTTP de autoridade. Entre APIs, o tenant deve continuar sendo comprovado pelo token e por autorização.

## Mensageria

Os building blocks não terão dependência de Kafka, Pub/Sub, RabbitMQ, Azure Service Bus ou outro broker.

Cada adapter de infraestrutura converte headers nativos para pares `string/string` e usa a library para:

1. extrair o snapshot recebido;
2. criar a Activity `Consumer` com o parent W3C;
3. abrir o contexto de execução;
4. processar a mensagem;
5. preservar headers em retry, DLQ e replay.

Na publicação:

1. cria-se a Activity `Producer`;
2. captura-se o contexto atual;
3. injeta-se o snapshot nos headers do broker.

## Outbox

Quando uma mensagem for persistida para publicação posterior, a Outbox deve armazenar os campos opcionais de tracing e os campos obrigatórios de contexto do fluxo:

- `correlation_id`;
- `tenant_id` para dados tenant-owned;
- `traceparent`;
- `tracestate`;
- `baggage`.

O relay deve:

1. restaurar o snapshot persistido como parent de uma Activity `Producer`;
2. capturar o contexto da nova Activity;
3. publicar os headers derivados desse novo span.

Capturar headers apenas durante o polling da Outbox quebraria a relação com a requisição ou comando original.

## Configuração OpenTelemetry

A library não define:

- `service.name`;
- exporter;
- collector;
- endpoint OTLP;
- sampling;
- backend APM;
- dashboards;
- alertas.

Cada executável configura esses elementos e registra os `ActivitySource` que utiliza.

## Multitenancy

A presença de `tenant_id` em mensagens não substitui autorização.

Consumidores devem tratar `tenant_id` como parte do contexto da mensagem e validar o ownership antes de acessar dados. APIs continuam usando a claim validada do token conforme ADR-0001.

`tenant_id` e `correlation_id` não devem ser labels de métricas devido à cardinalidade. Podem ser usados em logs e traces quando necessários para diagnóstico e protegidos contra exposição indevida.

## Consequências positivas

- Continuidade de trace entre HTTP, Outbox, broker e consumer.
- Headers e parsing consistentes entre serviços.
- Menor duplicação em adapters Kafka, Pub/Sub ou futuros brokers.
- Correlação operacional independente do sampling do trace.
- Contexto multitenant disponível em workers e jobs.
- Workers não dependem de ASP.NET Core.
- Troca de vendor APM sem alteração do contrato de propagação.

## Consequências negativas e custos

- Cria building blocks compartilhados que precisam de compatibilidade cuidadosa.
- Mudanças nos headers canônicos exigem revisão de todos os produtores e consumidores.
- Outbox passa a persistir metadados adicionais.
- Baggage exige governança para evitar PII e crescimento descontrolado.
- Adapters de cada broker ainda precisam converter seus tipos nativos.

## Alternativas consideradas

### Implementação local em cada serviço

Rejeitada para o novo projeto porque observabilidade e multitenancy serão requisitos transversais desde o início. A duplicação aumentaria o risco de drift.

### Library única dependente de ASP.NET Core

Rejeitada porque workers passariam a depender do runtime web sem necessidade.

### Dependência direta de Kafka ou outro broker

Rejeitada porque anteciparia uma escolha de infraestrutura ainda não tomada e reduziria a portabilidade.

### Usar somente `correlation_id`

Rejeitada porque não mantém a árvore de spans distribuída.

### Usar somente `TraceId`

Rejeitada porque correlação operacional precisa continuar disponível mesmo quando não houver Activity ou quando o trace não for amostrado.

## Validação

A implementação deve possuir testes para:

- round-trip dos headers;
- leitura case-insensitive;
- correlação inválida;
- continuidade do parent W3C;
- restauração de baggage;
- propagação de `X-Correlation-Id` em HTTP;
- ausência de `tenant_id` como header HTTP de autoridade;
- escopo de correlation e tenant no middleware web.

## Referências de implementação

- `src/BuildingBlocks/PetShop.Observability/`;
- `src/BuildingBlocks/PetShop.Observability.AspNetCore/`;
- `tests/BuildingBlocks/PetShop.Observability.Tests/`;
- `.agents/skills/configuring-opentelemetry-dotnet/SKILL.md`.
