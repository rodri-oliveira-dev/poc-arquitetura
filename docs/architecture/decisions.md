# Analise arquitetural e decisoes recomendadas

## Resumo executivo

A arquitetura atual esta mais proxima de Clean Architecture/DDD por microservico, mas nao e pura. Na pratica, e uma arquitetura hibrida e coerente com uma POC de microservicos: camadas internas nos servicos com dominio relevante, APIs HTTP e workers separados por processo, Pub/Sub/Outbox para consistencia eventual, Kafka legado opcional e Keycloak como provedor principal de identidade local.

A recomendacao e nao aumentar o numero de camadas agora. O melhor caminho e preservar a estrutura atual, corrigir assimetrias pontuais e fortalecer contratos/eventos/documentacao antes de qualquer reestruturacao.

## Avaliacao real por servico

### LedgerService

Camadas atuais: adequadas.

O servico tem complexidade suficiente para justificar separacao: endpoint protegido, idempotencia, transacao, dominio com invariantes, persistencia e Outbox com Pub/Sub principal e Kafka legado opcional. A separacao em `LedgerService.Api` e `LedgerService.Worker` ajuda escala independente, troubleshooting, readiness e observabilidade.

Excessos ou sinais de atencao:

- `CreateLancamentoService` concentra muitas responsabilidades de orquestracao.
- `OutboxMessage` no Domain e pragmatico, mas tecnicamente e um mecanismo de integracao/persistencia.
- Evento de integracao no Application e serializacao no caso de uso podem virar acoplamento se novos consumidores aparecerem.

Simplificacoes recomendadas:

- nao introduzir MediatR no Ledger apenas por simetria com Balance;
- nao criar interfaces para services sem segunda implementacao clara;
- extrair somente quando o caso de uso crescer ou quando testes ficarem ruidosos.

### BalanceService

Camadas atuais: adequadas, com algum overhead aceitavel.

Balance possui leitura HTTP, consumer Pub/Sub principal, adapter Kafka legado, DLQ, idempotencia de eventos e projecao. A separacao em `BalanceService.Api` e `BalanceService.Worker`, compartilhando Application/Domain/Infrastructure por composition roots explicitos, e justificavel.

Excessos ou sinais de atencao:

- MediatR e util, mas nao indispensavel no tamanho atual.
- As interfaces de servicos de leitura/consolidacao eram artificiais e foram removidas; os handlers MediatR concentram os casos de uso de consulta.
- `DefaultCurrency = "BRL"` no handler denuncia uma lacuna de contrato entre Ledger e Balance.

Simplificacoes recomendadas:

- manter MediatR se o time valoriza handlers e behaviors; nao expandir o padrao para tudo automaticamente;
- manter handlers de consulta diretos e revisar novas interfaces conforme surgirem necessidades reais;
- tratar currency como evolucao de contrato, nao como detalhe local permanente.

### Identidade

Keycloak e o caminho principal.

Keycloak emite JWT RS256 por OIDC e publica JWKS para validacao offline pelas APIs de negocio. O `Auth.Api` foi depreciado e permanece apenas como legado por overlay explicito. Enquanto o projeto legado existir, manter seus testes e o projeto unico e mais simples do que introduzir camadas artificiais.

Excessos ou sinais de atencao:

- o fallback `Auth.Api` nao deve voltar para a stack principal sem nova decisao;
- qualquer evolucao de identidade deve acontecer no Keycloak ou em outro IdP OIDC real.

Simplificacoes recomendadas:

- manter Keycloak como provedor principal;
- manter `Auth.Api` pequeno, testado e fora do compose principal enquanto houver necessidade de compatibilidade;
- remover completamente `Auth.Api` apenas em etapa futura dedicada.

## Problemas principais encontrados

- Inconsistencia de posicao das portas de persistencia: Ledger coloca repositories no Domain; Balance coloca em Application.
- Contrato de evento ainda depende de disciplina manual entre produtor e consumidor.
- Currency ausente em `LedgerEntryCreated.v1` foi tratada criando `LedgerEntryCreated.v2` com `currency` obrigatoria. O fallback `BRL` permanece somente para leitura de v1 legado.
- Readiness das APIs ainda mistura checks de infraestrutura no `Program.cs`; aceitavel enquanto validar apenas dependencias do trafego HTTP, mas pode crescer demais se novos checks forem adicionados.
- Rollout entre API antiga e Worker novo exige cuidado para evitar HostedServices duplicados publicando Outbox, consumindo Kafka ou processando pendencias simultaneamente.
- Alguma logica temporal usa `DateTime.Now`; Balance ja tem `IClock`, Ledger ainda nao.
- Observabilidade esta presente, mas tags e ActivitySource podem se espalhar pela Application se nao houver criterio.

## Principais excessos encontrados

- Risco de interfaces artificiais em services de Application com uma unica implementacao.
- MediatR no Balance pode ser mais estrutura do que necessidade atual, embora esteja bem aplicado.
- Outbox como entidade de Domain pode ser mais "arquitetural" que negocio.
- Auth.Api legado nao precisa ganhar as mesmas quatro camadas dos outros servicos.

## Principais simplificacoes recomendadas

- Manter Auth.Api legado em projeto unico enquanto ele existir.
- Nao padronizar MediatR em todos os servicos sem necessidade.
- Nao criar shared libraries para contratos antes de decidir governanca de versionamento.
- Nao quebrar `CreateLancamentoService` agora; observar crescimento e extrair com criterio.
- Documentar boundaries e contratos antes de mover arquivos.

## Riscos arquiteturais

- Evolucao de eventos pode quebrar consumidor sem testes de contrato/schema.
- Defaults locais, como currency `BRL`, podem virar regra implicita e dificil de desfazer.
- Duplicidade de padroes entre Ledger e Balance pode confundir contribuidores.
- Acoplamento operacional no `Program.cs` pode virar composicao dificil de testar.
- Auth.Api legado pode ser confundido com caminho operacional se voltar a aparecer na stack principal.
- Outbox/DLQ exigem operacao de reprocessamento; hoje a rotina e principalmente documentada/manual.

## Roadmap recomendado

### Quick wins

- Manter estes diagramas LikeC4 atualizados junto com ADRs relevantes.
- Manter testes de contrato para `LedgerEntryCreated.v2` validando payload e mapeamentos Pub/Sub/Kafka, preservando leitura de `LedgerEntryCreated.v1` legado.
- Documentar explicitamente o default de currency como divida, nao como regra final.
- Padronizar onde ficam portas de persistencia nos proximos servicos; nao mover agora sem refactor dedicado.

### Medio prazo

- Introduzir clock abstrato no LedgerService para reduzir uso de `DateTime.Now`.
- Isolar montagem de evento/outbox se `CreateLancamentoService` crescer.
- Definir politica de evolucao de eventos: JSON Schema/contratos versionados ou schema registry se a POC virar baseline.
- Extrair readiness checks para componentes pequenos se os checks das APIs passarem de banco e dependencias diretas do trafego HTTP.

### Longo prazo

- Remover o projeto Auth.Api legado quando nao houver mais necessidade de compatibilidade.
- Definir estrategia de replay/re-drive de DLQ e reconstrucao de projecoes.
- Avaliar .NET Aspire apenas se a operacao local/orquestracao se tornar gargalo real.
- Evoluir orquestracao/deploy para tratar APIs e workers como unidades operacionais independentes, incluindo rollout sem duplicidade de HostedServices.

## Decisao recomendada

Adotar arquitetura minimalista e pragmatica, robusta onde a complexidade e real:

- LedgerService e BalanceService continuam com camadas `Api`, `Application`, `Domain` e `Infrastructure`.
- `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker` sao processos separados, com composition roots explicitos e `ServiceName` proprio.
- Auth.Api legado continua como projeto simples fora da stack principal enquanto existir.
- Boundaries devem ser reforcados por documentacao, testes de contrato e revisao de dependencias, nao por novas camadas preventivas.
- Refactors estruturais devem ser planejados em etapas pequenas, com motivo concreto e ADR propria quando alterarem a decisao.
