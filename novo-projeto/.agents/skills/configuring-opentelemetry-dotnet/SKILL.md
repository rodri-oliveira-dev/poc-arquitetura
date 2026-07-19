---
name: configuring-opentelemetry-dotnet
description: Use esta skill para revisar, configurar ou evoluir OpenTelemetry em APIs e workers .NET, incluindo traces, métricas, logs, OTLP, correlação e spans customizados. Não use para logging simples ou para adicionar pacotes sem necessidade concreta.
license: MIT
---

# Objetivo

Introduzir observabilidade com propósito claro, baixo acoplamento e cuidado com cardinalidade, custo e dados pessoais.

## Quando usar

- Tracing distribuído.
- Métricas customizadas.
- Exportação OTLP.
- Correlação entre frontend, API, worker, banco e integrações externas.
- `ActivitySource`, `Meter`, tags, baggage e propagação.
- Diagnóstico de latência, erro, throughput ou backlog.

## Processo

1. Leia `AGENTS.md`, configuração e instrumentação existentes.
2. Identifique o objetivo:
   - latência;
   - erro;
   - volume;
   - concorrência;
   - dependência externa;
   - processamento agendado;
   - conflito ou cancelamento.
3. Escolha entre log, trace, métrica ou combinação.
4. Verifique pacotes existentes antes de adicionar dependência.
5. Mantenha nomes de `ActivitySource`, `Meter` e métricas estáveis.
6. Use tags de baixa cardinalidade.
7. Propague contexto em chamadas HTTP e processamento assíncrono.
8. Adicione spans customizados somente para etapas arquiteturalmente relevantes.
9. Atualize documentação quando o contrato de observabilidade mudar.
10. Valide build, testes e exportação local quando disponível.

## Sugestões para o domínio

Métricas úteis podem incluir:

- agendamentos criados, cancelados e reagendados;
- conflitos de disponibilidade;
- tempo para confirmar um agendamento;
- atendimentos por estado;
- duração real versus prevista;
- falhas de notificação;
- reservas temporárias expiradas.

Evite usar como labels:

- identificador de tutor;
- identificador de pet;
- e-mail;
- telefone;
- correlation ID;
- appointment ID;
- mensagem completa de erro.

Esses identificadores podem aparecer em logs ou traces quando necessários e protegidos, mas não como dimensões de métricas.

## Segurança

- Não registre tokens, senhas, dados pessoais ou payloads completos.
- Não use baggage para transportar PII.
- Não exponha stack trace ao cliente.
- Não habilite exporter de console como padrão de produção.
- Não crie health check pesado que consulte grande volume de agenda.

## Restrições

- Não instalar instrumentação automática sem biblioteca correspondente em uso.
- Não criar uma métrica para cada evento sem pergunta operacional clara.
- Não usar tracing como substituto de logs ou métricas.
- Não trocar de APM ou vendor sem decisão explícita.

## Saída esperada

- objetivo observado;
- instrumentação adicionada ou corrigida;
- riscos de cardinalidade e privacidade;
- validações executadas;
- limitações conhecidas.
