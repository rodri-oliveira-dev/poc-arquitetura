# ADR-0018: Avaliar adocao incremental do .NET Aspire

## Status
Proposto

## Contexto

O projeto possui uma POC de microservicos .NET com `Auth.Api`, `LedgerService.Api`, `BalanceService.Api`, PostgreSQL por servico, Kafka, Outbox, DLQ, health/readiness e OpenTelemetry opcional. O fluxo local principal esta documentado via `compose.yaml`, scripts e README.

.NET Aspire pode melhorar a experiencia local com `AppHost`, dashboard, orquestracao de recursos e `ServiceDefaults`. Ao mesmo tempo, criaria uma segunda representacao da topologia, com risco de drift em relacao ao compose e aos scripts atuais.

## Decisao proposta

Adotar Aspire apenas de forma incremental e inicialmente local, com escopo explicito:

- criar um `AppHost` experimental para orquestracao de desenvolvimento;
- criar `ServiceDefaults` somente depois de mapear os defaults ja existentes nas APIs;
- manter o compose funcional ate uma decisao posterior de substituicao;
- nao usar o AppHost como definicao de deploy produtivo.

## Alternativas consideradas

- Nao adotar Aspire e manter somente `nerdctl compose`.
- Substituir imediatamente o compose por Aspire.
- Adotar apenas `ServiceDefaults`, sem AppHost.

## Consequencias positivas

- Melhor onboarding local.
- Dashboard centralizado para logs, health e traces.
- Topologia local tipada e mais facil de evoluir.
- Possibilidade de padronizar observabilidade e resiliencia.

## Consequencias negativas / trade-offs

- Aumenta quantidade de projetos e conceitos na solucao.
- Pode duplicar configuracao de portas, variaveis, health checks e recursos.
- Exige atualizacao de README, scripts e CI.
- Pode gerar falsa percepcao de preparo produtivo.

## Riscos

- Drift entre `compose.yaml` e `AppHost`.
- Perda acidental de hardening atual ao introduzir `ServiceDefaults`.
- Dificuldade de modelar Kafka/topicos/migrations com a mesma semantica atual.

## Proximos passos sugeridos

- Prototipar AppHost em branch separada.
- Definir matriz compose vs Aspire para recursos, portas, variaveis e health checks.
- Validar se os testes continuam independentes do AppHost.
- Atualizar README somente apos escolher o fluxo recomendado.
