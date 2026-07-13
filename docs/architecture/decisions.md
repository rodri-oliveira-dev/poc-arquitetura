# Analise arquitetural e decisoes recomendadas

## Resumo executivo

A arquitetura atual esta mais proxima de Clean Architecture/DDD por bounded context, mas nao e pura. Na pratica, e uma arquitetura hibrida e coerente com um projeto de estudos arquiteturais que nasceu como POC: camadas internas nos servicos com dominio relevante, APIs HTTP e workers separados por processo, Outbox com Kafka para consistencia eventual, Keycloak como IdP principal, IdentityService como bounded context de cadastro/vinculo local de usuarios, PaymentService como bounded context de pagamentos externos e AuditService como bounded context de auditoria funcional.

A recomendacao e nao aumentar o numero de camadas agora. O melhor caminho e preservar a estrutura atual, corrigir assimetrias pontuais e fortalecer contratos/eventos/documentacao antes de qualquer reestruturacao.

A organizacao de desenvolvimento usa `PocArquitetura.slnx` como solution
agregadora global e solutions contextuais para Ledger, Balance, Transfer,
Payment, Identity, Audit e Shared. Essa organizacao orienta build, testes e experiencia
local, mas nao implica separacao automatica de deployment, ownership,
repositorios, bancos ou topologia runtime.

## Avaliacao real por servico

### LedgerService

Camadas atuais: adequadas.

O servico tem complexidade suficiente para justificar separacao: endpoint protegido, idempotencia, transacao, dominio com invariantes, persistencia e Outbox publicada no Kafka. A separacao em `LedgerService.Api` e `LedgerService.Worker` ajuda escala independente, troubleshooting, readiness e observabilidade.

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

Balance possui leitura HTTP, consumer Kafka, DLQ, idempotencia de eventos e projecao. A separacao em `BalanceService.Api` e `BalanceService.Worker`, compartilhando Application/Domain/Infrastructure por composition roots explicitos, e justificavel.

Excessos ou sinais de atencao:

- MediatR e util, mas nao indispensavel no tamanho atual.
- As interfaces de servicos de leitura/consolidacao eram artificiais e foram removidas; os handlers MediatR concentram os casos de uso de consulta.
- O fallback `BRL` ainda existe apenas para leitura de `LedgerEntryCreated.v1` legado; o contrato atual `LedgerEntryCreated.v2` exige `currency`.

Simplificacoes recomendadas:

- manter MediatR se o time valoriza handlers e behaviors; nao expandir o padrao para tudo automaticamente;
- manter handlers de consulta diretos e revisar novas interfaces conforme surgirem necessidades reais;
- manter `LedgerEntryCreated.v2` como contrato atual com `currency` obrigatoria e tratar v1 como legado de compatibilidade.

### Identidade

Keycloak e o IdP principal; IdentityService e o bounded context de usuarios.

Keycloak emite JWT RS256 por OIDC e publica JWKS para validacao offline pelas APIs. O `IdentityService.Api` cria usuarios no Keycloak via Admin API, define senha no provider, gera `MerchantId`, persiste o vinculo local no schema `identity` e envia e-mail de boas-vindas por domain event depois do commit. O `Auth.Api` legado foi removido da stack operacional; referencias restantes ficam em ADRs e relatorios historicos.

Excessos ou sinais de atencao:

- um fallback local proprio de autenticacao nao deve voltar para a stack principal sem nova decisao;
- o IdentityService nao deve virar emissor de tokens nem absorver regras de autorizacao dos servicos financeiros;
- o envio atual de e-mail nao tem Outbox, retry duravel ou DLQ; ADR-0095 registra essa evolucao apenas como futura.

Simplificacoes recomendadas:

- manter Keycloak como provedor principal;
- manter IdentityService focado em cadastro, MerchantId, vinculo com Keycloak e e-mail de boas-vindas;
- manter Keycloak como unico emissor operacional local.

### PaymentService

Camadas atuais: adequadas para o escopo implementado.

O contexto isola pagamentos externos, state machine de Payment/Refund, ACL de
provider fake/Stripe, webhook assinado, Inbox duravel e integracao idempotente
com Ledger. A separacao em API e Worker e importante: o webhook apenas valida e
persiste entrada confiavel; processamento da Inbox e materializacao financeira
ficam fora do request HTTP.

Excessos ou sinais de atencao:

- as ADRs 0101-0105 nasceram como propostas, mas a branch atual ja contem a
  implementacao inicial; a documentacao de arquitetura deve refletir o runtime
  real e preservar ADRs como historico da decisao;
- `payment-service` e `payment-worker` sobem no Compose local padrao, mas ainda
  dependem de migrations aplicadas pelos scripts `scripts/local/start-stack.*`
  antes dos containers iniciarem;
- Payment nao deve ganhar Kafka financeiro proprio nem chamar Balance para
  "adiantar" saldo.

Simplificacoes recomendadas:

- manter o provider fake para testes e desenvolvimento local;
- manter Stripe atras da porta `IPaymentGateway`;
- evoluir refund parcial, reconciliacao ou eventos proprios apenas quando houver
  requisito e contrato claro.

### AuditService

Camadas atuais: adequadas, com uma ressalva operacional.

O contexto possui API HTTP canonica, schema `audit`, contrato agnostico ao
chamador e Worker Kafka opcional para `AuditRecordRequested.v1`. A implementacao
do Worker nao significa que Ledger, Balance, Transfer ou Payment ja produzam
eventos reais de auditoria; essa integracao continua dependente de decisao e
producer futuro.

Excessos ou sinais de atencao:

- representar o topico `audit.record.requested` sem avisar que nao ha producer
  real cria falsa sensacao de fluxo ativo;
- misturar auditoria funcional com logs/traces/metricas confundiria ownership;
- chamar AuditService sincronicamente de fluxos financeiros principais deve ser
  decisao explicita, nao atalho acidental.

Simplificacoes recomendadas:

- manter a API HTTP canonica como contrato ativo;
- manter o Worker como caminho opcional documentado;
- conectar producers por Outbox + Kafka somente quando houver primeiro fluxo
  produtor definido e contrato versionado.

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

## Principais simplificacoes recomendadas

- Nao padronizar MediatR em todos os servicos sem necessidade.
- Nao criar shared libraries para contratos antes de decidir governanca de versionamento.
- Nao quebrar `CreateLancamentoService` agora; observar crescimento e extrair com criterio.
- Documentar boundaries e contratos antes de mover arquivos.

## Riscos arquiteturais

- Evolucao de eventos pode quebrar consumidor se novos contratos escaparem dos JSON Schemas, exemplos, testes e workflow de validacao.
- Defaults legados, como fallback `BRL` para `LedgerEntryCreated.v1`, podem ser confundidos com regra atual se a documentacao de depreciacao nao continuar clara.
- Duplicidade de padroes entre Ledger e Balance pode confundir contribuidores.
- Acoplamento operacional no `Program.cs` pode virar composicao dificil de testar.
- IdentityService pode ser confundido com IdP se a documentacao nao deixar claro que tokens continuam sendo emitidos pelo Keycloak.
- E-mail de boas-vindas no IdentityService e side effect pos-commit sem garantia duravel; isso e aceitavel para a POC, mas deve ser reavaliado se virar requisito critico.
- PaymentService pode ser confundido com fonte de fato financeiro se os diagramas nao mostrarem que Ledger continua dono do lancamento e Balance continua derivado apenas dos eventos do Ledger.
- AuditService pode parecer integrado aos fluxos financeiros se o consumer Kafka opcional for mostrado sem a ressalva de que ainda nao ha producers reais.
- Outbox/DLQ exigem operacao cuidadosa de reprocessamento; ja existem runbooks e casos de uso internos, mas ainda nao ha automacao operacional completa para todos os cenarios produtivos.
- Baseline produtivo GCP/seguranca foi consolidado como referencia arquitetural em [production-readiness.md](production-readiness.md), mas ainda precisa virar decisoes e automacoes especificas antes de tratar o projeto como referencia operacional fora do laboratorio local.
- DAST/ZAP segue sem workflow ou gate automatizado.
- Testes k6 ainda nao possuem thresholds p95/p99 formalizados.

## Roadmap recomendado

O roadmap consolidado por areas de maturidade fica em [docs/roadmap.md](../roadmap.md). Esta secao preserva a leitura pragmatica original desta analise.

### Quick wins

- Manter estes diagramas LikeC4 atualizados junto com ADRs relevantes.
- Manter testes de contrato para `LedgerEntryCreated.v2` validando payload e mapeamentos Kafka, preservando leitura de `LedgerEntryCreated.v1` legado.
- Manter documentada a diferenca entre `LedgerEntryCreated.v2` atual e fallback `BRL` apenas para v1 legado.
- Padronizar onde ficam portas de persistencia nos proximos servicos; nao mover agora sem refactor dedicado.
- Manter OpenAPI automatizado como parte da validacao de contrato HTTP: geracao, lint, drift e diff de breaking changes.
- Manter os diagramas do IdentityService sincronizados com as ADRs 0089-0095 e com o contrato `docs/openapi/identity.v1.json`.
- Manter os diagramas do PaymentService sincronizados com `docs/architecture/payment-service.md`, `docs/development/payment-api.md` e os fluxos Stripe/Inbox/Ledger.
- Manter AuditService separado de observabilidade tecnica e marcar claramente a ausencia de producers reais de auditoria.

### Medio prazo

- Remover usos novos ou residuais de tempo direto quando aparecerem, preservando `IClock`/`SystemClock` como padrao em Ledger e Balance.
- Isolar montagem de evento/outbox se `CreateLancamentoService` crescer.
- Evoluir a politica de eventos versionados ja baseada em JSON Schema, avaliando schema registry apenas se o projeto sair do laboratorio local para baseline operacional mais amplo.
- Extrair readiness checks para componentes pequenos se os checks das APIs passarem de banco e dependencias diretas do trafego HTTP.
- Evoluir as decisoes especificas a partir do baseline recomendado para secrets, TLS interno, workload identity, WAF, rate limits e scans de imagem.
- Automatizar DAST/ZAP em workflow somente quando houver decisao e ambiente adequado para esse gate.
- Formalizar thresholds k6 p95/p99 depois de obter linha de base local reprodutivel.

### Longo prazo

- Evoluir e-mail do IdentityService para Outbox/worker/DLQ apenas se houver necessidade concreta de entrega duravel ou reprocessamento operacional.
- Evoluir replay/redrive de DLQ e reconstrucao de projecoes de runbooks e casos de uso internos para operacao mais automatizada quando houver necessidade real.
- Avaliar .NET Aspire apenas se a operacao local/orquestracao se tornar gargalo real.
- Evoluir orquestracao/deploy para tratar APIs e workers como unidades operacionais independentes, incluindo rollout sem duplicidade de HostedServices.

## Decisao recomendada

Adotar arquitetura minimalista e pragmatica, robusta onde a complexidade e real:

- LedgerService e BalanceService continuam com camadas `Api`, `Application`, `Domain` e `Infrastructure`.
- IdentityService continua com camadas `Api`, `Application`, `Domain` e `Infrastructure`, sem Worker ou mensageria nesta etapa.
- PaymentService continua com `Api`, `Application`, `Domain`, `Infrastructure` e `Worker`, sem Balance direto e sem Kafka financeiro proprio.
- AuditService continua com `Api`, `Application`, `Domain`, `Infrastructure` e `Worker` opcional, sem producers reais nos demais contexts nesta etapa.
- `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker` sao processos separados, com composition roots explicitos e `ServiceName` proprio.
- Boundaries devem ser reforcados por documentacao, testes de contrato e revisao de dependencias, nao por novas camadas preventivas.
- Refactors estruturais devem ser planejados em etapas pequenas, com motivo concreto e ADR propria quando alterarem a decisao.
