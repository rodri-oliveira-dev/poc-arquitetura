# Analise arquitetural e decisoes recomendadas

## Resumo executivo

A arquitetura atual esta mais proxima de Clean Architecture/DDD por microservico, mas nao e pura. Na pratica, e uma arquitetura hibrida e coerente com uma POC de microservicos: camadas internas nos servicos com dominio relevante, Kafka/Outbox para consistencia eventual e Auth.Api simplificado.

A recomendacao e nao aumentar o numero de camadas agora. O melhor caminho e preservar a estrutura atual, corrigir assimetrias pontuais e fortalecer contratos/eventos/documentacao antes de qualquer reestruturacao.

## Avaliacao real por servico

### LedgerService

Camadas atuais: adequadas.

O servico tem complexidade suficiente para justificar separacao: endpoint protegido, idempotencia, transacao, dominio com invariantes, persistencia e Outbox com Kafka. A separacao ajuda testes e manutencao.

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

Balance possui leitura HTTP, consumer Kafka, DLQ, idempotencia de eventos e projecao. A separacao em Application/Domain/Infrastructure e justificavel.

Excessos ou sinais de atencao:

- MediatR e util, mas nao indispensavel no tamanho atual.
- Interfaces de servicos de leitura/consolidacao podem ser artificiais se existirem apenas por padrao.
- `DefaultCurrency = "BRL"` no handler denuncia uma lacuna de contrato entre Ledger e Balance.

Simplificacoes recomendadas:

- manter MediatR se o time valoriza handlers e behaviors; nao expandir o padrao para tudo automaticamente;
- revisar interfaces sem multiplas implementacoes quando houver refactor dedicado;
- tratar currency como evolucao de contrato, nao como detalhe local permanente.

### Auth.Api

Camadas atuais: adequadas por serem simples.

Auth.Api e uma API de autenticacao de POC com endpoints, emissor JWT, chave RSA, JWKS e hardening. Projeto unico e uma boa escolha. Separar em Domain/Application/Infrastructure agora seria excesso.

Excessos ou sinais de atencao:

- regra de login no endpoint e aceitavel para POC, mas nao deve crescer para um identity provider proprio.
- persistencia de chave em arquivo e coerente com compose local, nao com identidade corporativa.

Simplificacoes recomendadas:

- manter Auth.Api pequeno;
- priorizar Keycloak/OIDC externo se o dominio de identidade ficar real;
- nao criar camadas internas enquanto nao houver persistencia, usuarios reais, refresh token ou lifecycle de credenciais.

## Problemas principais encontrados

- Inconsistencia de posicao das portas de persistencia: Ledger coloca repositories no Domain; Balance coloca em Application.
- Contrato de evento ainda depende de disciplina manual entre produtor e consumidor.
- Currency ausente em `LedgerEntryCreated.v1` gera default no Balance, com risco de semantica incorreta.
- Readiness mistura checks de infraestrutura no `Program.cs`; aceitavel, mas pode crescer demais se novos checks forem adicionados.
- Alguma logica temporal usa `DateTime.Now`; Balance ja tem `IClock`, Ledger ainda nao.
- Observabilidade esta presente, mas tags e ActivitySource podem se espalhar pela Application se nao houver criterio.

## Principais excessos encontrados

- Risco de interfaces artificiais em services de Application com uma unica implementacao.
- MediatR no Balance pode ser mais estrutura do que necessidade atual, embora esteja bem aplicado.
- Outbox como entidade de Domain pode ser mais "arquitetural" que negocio.
- Auth.Api nao precisa ganhar as mesmas quatro camadas dos outros servicos.

## Principais simplificacoes recomendadas

- Manter Auth.Api em projeto unico.
- Nao padronizar MediatR em todos os servicos sem necessidade.
- Nao criar shared libraries para contratos antes de decidir governanca de versionamento.
- Nao quebrar `CreateLancamentoService` agora; observar crescimento e extrair com criterio.
- Documentar boundaries e contratos antes de mover arquivos.

## Riscos arquiteturais

- Evolucao de eventos pode quebrar consumidor sem testes de contrato/schema.
- Defaults locais, como currency `BRL`, podem virar regra implicita e dificil de desfazer.
- Duplicidade de padroes entre Ledger e Balance pode confundir contribuidores.
- Acoplamento operacional no `Program.cs` pode virar composicao dificil de testar.
- POC de Auth pode ser confundida com solucao de identidade pronta para producao.
- Outbox/DLQ exigem operacao de reprocessamento; hoje a rotina e principalmente documentada/manual.

## Roadmap recomendado

### Quick wins

- Manter estes diagramas LikeC4 atualizados junto com ADRs relevantes.
- Criar teste de contrato para `LedgerEntryCreated.v1` validando payload e headers obrigatorios.
- Documentar explicitamente o default de currency como divida, nao como regra final.
- Padronizar onde ficam portas de persistencia nos proximos servicos; nao mover agora sem refactor dedicado.

### Medio prazo

- Introduzir clock abstrato no LedgerService para reduzir uso de `DateTime.Now`.
- Isolar montagem de evento/outbox se `CreateLancamentoService` crescer.
- Definir politica de evolucao de eventos: JSON Schema/contratos versionados ou schema registry se a POC virar baseline.
- Extrair readiness checks para componentes pequenos se os checks passarem de DB/Kafka basico.

### Longo prazo

- Substituir Auth.Api por provedor OIDC real, como ja proposto em ADR.
- Definir estrategia de replay/re-drive de DLQ e reconstrucao de projecoes.
- Avaliar .NET Aspire apenas se a operacao local/orquestracao se tornar gargalo real.
- Avaliar separar workers de API se escala operacional exigir ciclos independentes de deploy/replicas.

## Decisao recomendada

Adotar arquitetura minimalista e pragmatica, robusta onde a complexidade e real:

- LedgerService e BalanceService continuam com camadas `Api`, `Application`, `Domain` e `Infrastructure`.
- Auth.Api continua como servico simples em projeto unico.
- Boundaries devem ser reforcados por documentacao, testes de contrato e revisao de dependencias, nao por novas camadas preventivas.
- Refactors estruturais devem ser planejados em etapas pequenas, com motivo concreto e ADR propria quando alterarem a decisao.
