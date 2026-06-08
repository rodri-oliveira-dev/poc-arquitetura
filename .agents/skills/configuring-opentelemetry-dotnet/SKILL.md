---
name: configuring-opentelemetry-dotnet
description: Use esta skill para revisar, configurar ou evoluir OpenTelemetry em APIs e workers .NET deste repositorio, incluindo traces, metricas, logs, OTLP, correlacao, spans customizados e troubleshooting de observabilidade. Nao use para logging simples sem tracing/metricas ou para instalar pacotes sem necessidade concreta.
license: MIT
---

# Objetivo

Orientar mudancas de OpenTelemetry neste repositorio sem contrariar Central Package Management, arquitetura existente ou configuracoes ja implementadas.

Esta skill deve ser usada como complemento ao `AGENTS.md`. Em caso de conflito, prevalecem `AGENTS.md`, ADRs, `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig` e os padroes locais do projeto.

# Quando usar

- Adicionar, revisar ou diagnosticar tracing distribuido.
- Adicionar, revisar ou diagnosticar metricas customizadas.
- Configurar ou revisar exportacao OTLP.
- Corrigir correlacao entre APIs, workers, Pub/Sub, Kafka, Outbox ou chamadas HTTP.
- Revisar `ActivitySource`, `Meter`, tags, baggage, traceparent ou propagation.
- Avaliar impacto de observabilidade em performance, custo, cardinalidade ou seguranca.

# Quando nao usar

- Logging simples com `ILogger`, sem tracing ou metricas.
- Mudancas puramente funcionais sem impacto em observabilidade.
- Instalacao casual de pacotes OpenTelemetry sem lacuna comprovada.
- Troca de APM/vendor sem decisao explicita.

# Regras obrigatorias

- Nao adicione `Version=` em `PackageReference`; o repositorio usa Central Package Management.
- Antes de adicionar pacote, verifique `Directory.Packages.props` e os `.csproj` existentes.
- Nao instale pacotes apenas porque exemplos externos recomendam.
- Nao adicione Console Exporter em producao por padrao; use apenas quando o projeto ja permitir ou quando for explicitamente uma configuracao local/dev.
- Nao registre segredos, tokens, payloads completos, dados sensiveis ou informacoes pessoais em tags, logs ou baggage.
- Evite alta cardinalidade em metricas e tags, como IDs unicos, e-mails, tokens, payloads, correlation ids como label de metrica ou mensagens de erro completas.
- Para workers, nao introduza endpoint HTTP de health/readiness apenas para observabilidade. Prefira metricas, logs, traces, exit code, retry policy e alertas, salvo exigencia operacional clara.
- Para readiness ou metricas observaveis, evite consultas pesadas a banco, fila, DLQ ou views de grande volume em tempo real.

# Processo

1. Leia `AGENTS.md`, `docs/observability.md`, `docs/adrs/`, `Directory.Packages.props` e os arquivos de composicao do servico afetado.
2. Identifique se a mudanca envolve API, worker, producer, consumer, Outbox, DLQ ou integracao externa.
3. Verifique a instrumentacao existente antes de propor pacotes ou extensoes novas.
4. Defina o objetivo da observabilidade: diagnostico de latencia, correlacao, erro, throughput, backlog, DLQ, processamento ou dependencia externa.
5. Escolha entre log, trace, metrica ou combinacao dos tres. Nao use tracing para tudo.
6. Mantenha nomes de `ActivitySource`, `Meter` e metricas estaveis.
7. Use tags de baixa cardinalidade e nomes coerentes com o dominio tecnico do projeto.
8. Se alterar contratos de observabilidade relevantes, atualize documentacao ou ADR quando adequado.
9. Valide build/testes proporcionais ao impacto.

# Pacotes

Antes de adicionar pacote, confira se ele ja existe no projeto ou no gerenciamento central. Pacotes comuns neste repositorio incluem, conforme necessidade real:

- `OpenTelemetry`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Exporter.Console`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Instrumentation.Runtime`

Adicione instrumentacoes automaticas adicionais somente quando houver biblioteca correspondente em uso e beneficio claro, por exemplo ASP.NET Core, HttpClient, Npgsql ou runtime.

# Saida esperada

- Mudanca minima e coerente com a observabilidade existente.
- Explicacao do que passou a ser observado e por qual motivo.
- Riscos de cardinalidade, custo, ruido ou dados sensiveis avaliados.
- Validacoes executadas ou motivo para nao executar.
