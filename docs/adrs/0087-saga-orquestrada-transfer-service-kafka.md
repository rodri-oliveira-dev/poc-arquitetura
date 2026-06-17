# ADR-0087: Saga orquestrada de transferencias entre merchants com TransferService e Kafka

## Status
Proposto

## Data
2026-06-17

## Contexto
O projeto possui `LedgerService` como dono das regras de lancamento, `BalanceService` como projecao eventual dos eventos do Ledger e mensageria com Outbox, DLQ, Pub/Sub principal e Kafka legado opcional em fluxos existentes.

Para estudar consistencia distribuida entre microservicos sem mover regras de lancamento para fora do Ledger, sera criado um novo bounded context chamado `TransferService`. O caso de uso planejado e transferir valor de um merchant origem para um merchant destino.

Esse estudo precisa deixar explicito o padrao de coordenacao escolhido, o transporte usado no fluxo, os limites entre contexts e a estrategia de idempotencia antes de qualquer codigo funcional.

## Problema
Uma transferencia entre merchants exige coordenar pelo menos tres etapas logicas:

- criar o debito no merchant origem;
- criar o credito no merchant destino;
- compensar o debito quando o credito nao puder ser concluido.

Se essa coordenacao ficar espalhada por eventos reagidos independentemente por varios servicos, o fluxo tende a se aproximar de uma Saga Coreografada, dificultando leitura de estado, diagnostico, observabilidade e decisao de compensacao. Ao mesmo tempo, o `LedgerService` nao deve perder a propriedade das regras de lancamento, e o `BalanceService` nao deve assumir decisao transacional ou de Saga.

## Decisao
Implementar o estudo como uma Saga Orquestrada no bounded context `TransferService`.

O `TransferService` sera o orquestrador da Saga. Ele mantera o estado da transferencia, decidira a proxima etapa, solicitara comandos ao `LedgerService.Api` e publicara eventos logicos da Saga por Kafka usando Outbox transacional.

O `LedgerService` continuara sendo o dono das regras de lancamento. O fluxo pode chamar o `LedgerService.Api` para comandos de debito, credito e compensacao, mas a validacao e a persistencia dos lancamentos permanecem no Ledger.

O `BalanceService` nao participa da decisao da Saga. Ele continua sendo uma projecao eventual derivada dos eventos do Ledger e nao deve receber responsabilidade de aprovar, rejeitar, compensar ou concluir transferencias.

Kafka sera o transporte explicito deste estudo de Saga. Pub/Sub nao deve ser usado neste fluxo. Esta restricao e intencional para estudar Kafka message key, ordenacao por chave, topicos dedicados de Saga, DLQ de aplicacao e observabilidade de eventos de orquestracao.

Esta decisao mantem uma Saga Orquestrada, nao uma Saga Coreografada. Os eventos da Saga servem para observabilidade, auditoria, integracao eventual e diagnostico; eles nao transferem para outros servicos a responsabilidade de decidir o proximo passo da transferencia.

## Bounded contexts e responsabilidades

### TransferService
- Recebe solicitacoes de transferencia.
- Persiste estado da Saga e historico minimo de transicoes.
- Executa worker assincrono para processar etapas pendentes.
- Chama `LedgerService.Api` para criar debito, criar credito e solicitar compensacao.
- Usa Outbox transacional para publicar eventos logicos da Saga no Kafka.
- Publica eventos de observabilidade da Saga e falhas definitivas em DLQ de aplicacao.
- Aplica idempotencia por etapa da Saga.

### LedgerService
- Continua dono das regras de lancamento.
- Valida comandos de debito, credito e compensacao.
- Persiste lancamentos conforme as invariantes do dominio do Ledger.
- Publica seus eventos financeiros proprios para manter consumidores existentes.

### BalanceService
- Continua como projecao eventual dos eventos do Ledger.
- Nao decide fluxo da transferencia.
- Nao consome eventos de Saga para alterar saldo diretamente.
- Nao participa de compensacao ou conclusao da Saga.

## Endpoints planejados

O `TransferService.Api` deve expor uma superficie HTTP pequena:

| Metodo | Rota | Responsabilidade |
| --- | --- | --- |
| `POST` | `/api/v1/transferencias` | Solicitar uma transferencia entre merchant origem e merchant destino. |
| `GET` | `/api/v1/transferencias/{transferenciaId}` | Consultar estado atual, dados principais e diagnostico resumido da transferencia. |

O `POST /api/v1/transferencias` deve registrar a solicitacao e retornar resposta assincrona, sem tentar completar debito e credito no request HTTP. O processamento da Saga pertence ao worker do `TransferService`.

## Estados planejados da Saga

| Estado | Significado |
| --- | --- |
| `Pending` | Transferencia registrada, ainda nao reclamada pelo worker. |
| `Processing` | Worker iniciou o processamento da Saga. |
| `DebitCreating` | Orquestrador esta solicitando ou aguardando criacao do debito no Ledger. |
| `DebitCreated` | Debito foi criado com sucesso no Ledger. |
| `CreditCreating` | Orquestrador esta solicitando ou aguardando criacao do credito no Ledger. |
| `Completed` | Debito e credito foram criados, encerrando a transferencia com sucesso. |
| `CompensationRequested` | Credito falhou apos debito criado, e a compensacao do debito foi solicitada. |
| `Compensated` | Debito foi compensado com sucesso. |
| `Failed` | Saga terminou por falha definitiva, com ou sem compensacao possivel. |
| `Rejected` | Solicitacao foi rejeitada antes de iniciar ou concluir etapas financeiras. |

As transicoes devem ser persistidas de forma idempotente e observavel. Estados finais planejados: `Completed`, `Compensated`, `Failed` e `Rejected`.

## Eventos logicos planejados

Os eventos abaixo pertencem ao bounded context `TransferService` e representam a evolucao da Saga, nao os eventos financeiros finais do Ledger:

| Evento | Quando publicar |
| --- | --- |
| `TransferenciaSolicitada.v1` | A solicitacao foi registrada e a Saga entrou em `Pending`. |
| `TransferenciaDebitoCriado.v1` | O debito foi criado no Ledger e a Saga entrou em `DebitCreated`. |
| `TransferenciaCreditoCriado.v1` | O credito foi criado no Ledger. |
| `TransferenciaConcluida.v1` | A transferencia foi concluida com sucesso. |
| `TransferenciaCompensacaoSolicitada.v1` | A compensacao do debito foi solicitada apos falha no credito. |
| `TransferenciaCompensada.v1` | A compensacao foi concluida. |
| `TransferenciaFalhou.v1` | A Saga falhou de forma definitiva ou nao recuperavel. |

Contratos JSON Schema, exemplos e testes de contrato devem ser criados apenas quando o runtime do estudo for implementado.

## Topicos Kafka sugeridos

| Topico | Evento principal |
| --- | --- |
| `transfer.transferencia.solicitada` | `TransferenciaSolicitada.v1` |
| `transfer.transferencia.debito-criado` | `TransferenciaDebitoCriado.v1` |
| `transfer.transferencia.credito-criado` | `TransferenciaCreditoCriado.v1` |
| `transfer.transferencia.concluida` | `TransferenciaConcluida.v1` |
| `transfer.transferencia.compensacao-solicitada` | `TransferenciaCompensacaoSolicitada.v1` |
| `transfer.transferencia.compensada` | `TransferenciaCompensada.v1` |
| `transfer.transferencia.falhou` | `TransferenciaFalhou.v1` |
| `transfer.transferencia.dlq` | Mensagens invalidas ou falhas definitivas de publicacao/consumo. |

## Message key

Todos os eventos de uma mesma Saga devem usar `transferenciaId` como Kafka message key.

Essa chave preserva a ordenacao logica dos eventos da mesma transferencia dentro das garantias de particionamento do Kafka. A ordenacao global entre transferencias diferentes nao e requisito deste estudo.

## Idempotencia por etapa

Cada etapa com efeito externo deve possuir chave de idempotencia propria:

| Etapa | Chave de idempotencia planejada |
| --- | --- |
| Criacao do debito | `transferencia:{sagaId}:debit` |
| Criacao do credito | `transferencia:{sagaId}:credit` |
| Compensacao do debito | `transferencia:{sagaId}:compensate-debit` |

Essas chaves devem ser usadas nas chamadas ao `LedgerService.Api` quando o contrato HTTP suportar idempotencia por chave. O `TransferService` tambem deve persistir a execucao das etapas para evitar repeticao indevida em retries do worker.

## Outbox transacional

O `TransferService` deve usar Outbox transacional para publicar eventos no Kafka.

Cada mudanca relevante de estado da Saga e o respectivo evento logico devem ser persistidos na mesma transacao do banco do `TransferService`. A publicacao no Kafka deve acontecer de forma assincrona por worker, com semantica de entrega pelo menos uma vez, retry controlado e registro de falhas.

## Worker assincrono

O processamento da Saga deve ser executado por worker do `TransferService`, separado da responsabilidade do request HTTP.

O worker deve:

- reclamar transferencias pendentes ou etapas recuperaveis de forma concorrente segura;
- chamar o `LedgerService.Api` com timeouts e politica de retry proporcionais;
- registrar transicoes de estado antes de publicar eventos;
- publicar eventos via Outbox/Kafka;
- evitar processamento duplicado por idempotencia de etapa;
- encaminhar falhas definitivas para DLQ de aplicacao com contexto suficiente para diagnostico.

## DLQ de aplicacao

Mensagens invalidas ou falhas definitivas de publicacao/consumo devem ir para DLQ de aplicacao.

A DLQ deve preservar:

- payload original;
- event type;
- correlation id;
- causa do erro;
- timestamp.

A DLQ da Saga nao substitui a necessidade de corrigir a causa raiz, nem deve ser usada como mecanismo normal de controle de fluxo. Reprocessamento, descarte ou compensacao operacional exigem decisao explicita em etapa futura.

## Observabilidade

Os eventos de Saga e sua observabilidade devem ser publicados via Kafka. O fluxo nao usa Pub/Sub.

O `TransferService` deve propagar correlation id nas chamadas HTTP para o `LedgerService.Api` e nos headers Kafka dos eventos de Saga. A implementacao futura deve preservar rastreabilidade entre request HTTP, estado persistido da Saga, chamadas ao Ledger, mensagens Outbox, publicacao Kafka e eventuais registros de DLQ.

## Consequencias

### Beneficios
- Centraliza a decisao da transferencia no `TransferService`, facilitando auditoria e diagnostico.
- Preserva o `LedgerService` como dono das regras de lancamento.
- Mantem o `BalanceService` simples como projecao eventual.
- Permite estudar Kafka explicitamente sem misturar Pub/Sub no mesmo fluxo.
- Evita transformar eventos de Saga em coreografia acidental.
- Cria base clara para idempotencia por etapa e compensacao.

### Trade-offs / custos
- O `TransferService` passa a conhecer o fluxo de coordenacao e as chamadas HTTP necessarias ao Ledger.
- A Saga Orquestrada cria um ponto central de decisao que precisa de persistencia, retry, timeout e operacao cuidadosa.
- Kafka volta a aparecer como transporte principal em um estudo especifico, mesmo com Pub/Sub sendo o provider principal da POC para os fluxos atuais.
- Contratos HTTP do Ledger podem precisar evoluir para suportar comandos explicitos de debito, credito e compensacao com idempotencia adequada.

### Riscos
- Acoplamento indevido se regras de lancamento forem movidas para o `TransferService`.
- Ambiguidade operacional se eventos de Saga forem tratados como fonte de saldo pelo `BalanceService`.
- Reprocessamento incorreto se idempotencia por etapa nao for persistida e testada.
- Compensacao parcial se o debito for criado e a compensacao falhar de forma definitiva.
- Observabilidade insuficiente se correlation id e headers Kafka nao forem preservados.

## Alternativas consideradas

1. **Saga Coreografada por eventos entre servicos**
   - Rejeitada para este estudo. A coreografia distribui a decisao do fluxo, dificulta enxergar o estado central da transferencia e aumenta risco de o `BalanceService` ou outros consumidores assumirem responsabilidade de orquestracao.

2. **TransferService criar lancamentos diretamente no banco do Ledger**
   - Rejeitada. Isso violaria ownership do `LedgerService` sobre regras de lancamento, persistencia e invariantes do dominio.

3. **BalanceService participar da decisao da Saga**
   - Rejeitada. O `BalanceService` deve continuar como leitura/projecao eventual e nao como participante de decisao transacional.

4. **Usar Pub/Sub no fluxo da Saga**
   - Rejeitada para este estudo. O objetivo e estudar Saga Orquestrada com Kafka explicito, message key, topicos dedicados e DLQ de aplicacao nesse transporte.

5. **Executar a transferencia completa no request HTTP**
   - Rejeitada. A transferencia deve ser assincrona para permitir retry, compensacao, observabilidade e recuperacao por worker.

## Fora do escopo
- Implementar projetos `TransferService.Api`, `TransferService.Worker`, `TransferService.Application`, `TransferService.Domain` ou `TransferService.Infrastructure`.
- Criar migrations, tabelas, contratos JSON Schema, producers, consumers ou endpoints reais.
- Alterar contratos atuais do `LedgerService.Api`.
- Alterar `BalanceService`.
- Atualizar diagramas LikeC4 antes de existir estrutura implementada ou especificacao visual dedicada.
- Executar testes de carga ou alterar cenarios k6.

## Validacao esperada em etapa futura
Quando o runtime for implementado, a validacao devera cobrir:

- testes de dominio e aplicacao para transicoes validas e invalidas da Saga;
- testes de idempotencia por etapa;
- testes de integracao para persistencia de estado e Outbox;
- testes de contrato HTTP do `TransferService.Api`;
- testes de contrato dos eventos `Transferencia*.v1`;
- testes de chamada ao `LedgerService.Api` com idempotencia;
- testes de publicacao Kafka e DLQ de aplicacao;
- revisao de observabilidade, correlation id e headers Kafka.
