# Analise arquitetural e decisoes recomendadas

## Resumo executivo

A arquitetura atual esta mais proxima de Clean Architecture/DDD por microservico, mas nao e pura. Na pratica, e uma arquitetura hibrida e coerente com um projeto de estudos arquiteturais que nasceu como POC: camadas internas nos servicos com dominio relevante, APIs HTTP e workers separados por processo, Pub/Sub/Outbox para consistencia eventual, Kafka legado opcional e Keycloak como provedor principal de identidade local.

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
- O fallback `BRL` ainda existe apenas para leitura de `LedgerEntryCreated.v1` legado; o contrato atual `LedgerEntryCreated.v2` exige `currency`.

Simplificacoes recomendadas:

- manter MediatR se o time valoriza handlers e behaviors; nao expandir o padrao para tudo automaticamente;
- manter handlers de consulta diretos e revisar novas interfaces conforme surgirem necessidades reais;
- manter `LedgerEntryCreated.v2` como contrato atual com `currency` obrigatoria e tratar v1 como legado de compatibilidade.

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
- Contratos de eventos ja possuem JSON Schemas, exemplos, documentacao e workflow de validacao; o risco remanescente esta em manter governanca de versao e compatibilidade conforme novos consumidores aparecerem.
- Currency ausente em `LedgerEntryCreated.v1` foi tratada criando `LedgerEntryCreated.v2` com `currency` obrigatoria. O fallback `BRL` permanece somente para leitura de v1 legado.
- Readiness das APIs ainda mistura checks de infraestrutura no `Program.cs`; aceitavel enquanto validar apenas dependencias do trafego HTTP, mas pode crescer demais se novos checks forem adicionados.
- Rollout entre API antiga e Worker novo exige cuidado para evitar HostedServices duplicados publicando Outbox, consumindo Kafka ou processando pendencias simultaneamente.
- Balance e Ledger possuem `IClock`/`SystemClock`; ainda vale vigiar novos usos diretos de `DateTime.Now` ou `DateTime.UtcNow` para nao reabrir acoplamento temporal.
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

- Evolucao de eventos pode quebrar consumidor se novos contratos escaparem dos JSON Schemas, exemplos, testes e workflow de validacao.
- Defaults legados, como fallback `BRL` para `LedgerEntryCreated.v1`, podem ser confundidos com regra atual se a documentacao de depreciacao nao continuar clara.
- Duplicidade de padroes entre Ledger e Balance pode confundir contribuidores.
- Acoplamento operacional no `Program.cs` pode virar composicao dificil de testar.
- Auth.Api legado pode ser confundido com caminho operacional se voltar a aparecer na stack principal.
- Outbox/DLQ exigem operacao cuidadosa de reprocessamento; ja existem runbooks e casos de uso internos, mas ainda nao ha automacao operacional completa para todos os cenarios produtivos.
- Baseline produtivo GCP/seguranca foi consolidado como referencia arquitetural em [production-readiness.md](production-readiness.md), mas ainda precisa virar decisoes e automacoes especificas antes de tratar o projeto como referencia operacional fora do laboratorio local.
- DAST/ZAP segue sem workflow ou gate automatizado.
- Testes k6 ainda nao possuem thresholds p95/p99 formalizados.

## Roadmap recomendado

O roadmap consolidado por areas de maturidade fica em [docs/roadmap.md](../roadmap.md). Esta secao preserva a leitura pragmatica original desta analise.

### Quick wins

- Manter estes diagramas LikeC4 atualizados junto com ADRs relevantes.
- Manter testes de contrato para `LedgerEntryCreated.v2` validando payload e mapeamentos Pub/Sub/Kafka, preservando leitura de `LedgerEntryCreated.v1` legado.
- Manter documentada a diferenca entre `LedgerEntryCreated.v2` atual e fallback `BRL` apenas para v1 legado.
- Padronizar onde ficam portas de persistencia nos proximos servicos; nao mover agora sem refactor dedicado.
- Manter OpenAPI automatizado como parte da validacao de contrato HTTP: geracao, lint, drift e diff de breaking changes.

### Medio prazo

- Remover usos novos ou residuais de tempo direto quando aparecerem, preservando `IClock`/`SystemClock` como padrao em Ledger e Balance.
- Isolar montagem de evento/outbox se `CreateLancamentoService` crescer.
- Evoluir a politica de eventos versionados ja baseada em JSON Schema, avaliando schema registry apenas se o projeto sair do laboratorio local para baseline operacional mais amplo.
- Extrair readiness checks para componentes pequenos se os checks das APIs passarem de banco e dependencias diretas do trafego HTTP.
- Evoluir as decisoes especificas a partir do baseline recomendado para secrets, TLS interno, workload identity, WAF, rate limits e scans de imagem.
- Automatizar DAST/ZAP em workflow somente quando houver decisao e ambiente adequado para esse gate.
- Formalizar thresholds k6 p95/p99 depois de obter linha de base local reprodutivel.

### Longo prazo

- Remover o projeto Auth.Api legado quando nao houver mais necessidade de compatibilidade.
- Evoluir replay/redrive de DLQ e reconstrucao de projecoes de runbooks e casos de uso internos para operacao mais automatizada quando houver necessidade real.
- Avaliar .NET Aspire apenas se a operacao local/orquestracao se tornar gargalo real.
- Evoluir orquestracao/deploy para tratar APIs e workers como unidades operacionais independentes, incluindo rollout sem duplicidade de HostedServices.

## Decisao recomendada

Adotar arquitetura minimalista e pragmatica, robusta onde a complexidade e real:

- LedgerService e BalanceService continuam com camadas `Api`, `Application`, `Domain` e `Infrastructure`.
- `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker` sao processos separados, com composition roots explicitos e `ServiceName` proprio.
- Auth.Api legado continua como projeto simples fora da stack principal enquanto existir.
- Boundaries devem ser reforcados por documentacao, testes de contrato e revisao de dependencias, nao por novas camadas preventivas.
- Refactors estruturais devem ser planejados em etapas pequenas, com motivo concreto e ADR propria quando alterarem a decisao.
