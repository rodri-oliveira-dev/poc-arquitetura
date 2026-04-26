# ADR-0033: Governanca de documentacao operacional

## Status
Aceito

## Data
2026-04-26

## Contexto
O README referencia documentacao operacional em `docs/observability.md`, e o repositorio ja possui ADRs para observabilidade, readiness, Kafka, DLQ, Outbox, seguranca e execucao local.

Mesmo com esses registros, parte do inventario operacional estava distribuida entre README e ADRs, o que aumenta o risco de drift quando uma mudanca altera endpoints operacionais, topicos, configuracao local, traces, metricas ou comportamento de Outbox/DLQ.

## Decisao
Adotar `docs/observability.md` como documento operacional minimo obrigatorio para a POC.

Docs obrigatorios:

- `README.md`: visao geral, setup local, comandos principais, contratos publicos e links para documentacao operacional.
- `docs/observability.md`: inventario operacional minimo, incluindo health, readiness, logs, metricas, tracing, Kafka, DLQ, Outbox e configuracao local.
- `docs/adrs/README.md`: indice das decisoes arquiteturais.
- `docs/adrs/NNNN-*.md`: registro de decisoes arquiteturais aceitas, propostas ou substituidas.

Responsaveis:

- Quem alterar comportamento operacional deve atualizar a documentacao afetada no mesmo ajuste.
- Quem revisar a mudanca deve validar se README, `docs/observability.md` e ADRs continuam coerentes.
- Mudancas em codigo, compose, configuracao, Kafka, Outbox, DLQ, health/readiness, logs, metricas ou tracing devem ser avaliadas explicitamente quanto a impacto documental.

Criterios de atualizacao:

- Atualizar `docs/observability.md` quando mudar endpoint operacional, readiness, health, campos de log/correlacao, traces, metricas, topicos Kafka, headers, DLQ, Outbox, retry, backoff, lock, portas, variaveis de ambiente ou fluxo de validacao local.
- Atualizar `README.md` quando mudar setup local, portas, comandos, contratos publicos, links ou instrucoes de uso.
- Criar ou atualizar ADR quando a mudanca envolver decisao arquitetural, padrao tecnico, contrato entre servicos, persistencia, mensageria, observabilidade, seguranca, resiliencia, integracao externa, estrutura de projeto ou alteracao relevante de comportamento.
- Nao criar ADR para correcao trivial, ajuste mecanico ou documentacao de comportamento existente sem decisao nova.

Relacao com mudancas arquiteturais:

- ADRs registram o motivo e os trade-offs da decisao.
- `docs/observability.md` registra como operar e validar o comportamento decidido.
- README aponta para o documento operacional e mantem o caminho feliz de execucao.

Arquivos afetados por esta decisao:

- `README.md`
- `docs/observability.md`
- `docs/adrs/README.md`
- `docs/adrs/0033-governanca-documentacao-operacional.md`

## Consequencias

### Beneficios
- Reduz drift entre codigo, compose, README e ADRs.
- Deixa claro onde operadores e desenvolvedores encontram o inventario operacional minimo.
- Torna obrigatoria a revisao documental em mudancas com impacto operacional.
- Ajuda a diferenciar decisao arquitetural de instrucao operacional.

### Trade-offs / custos
- Pequenas mudancas operacionais passam a exigir revisao documental explicita.
- O documento operacional precisa ser mantido junto com as ADRs para nao virar duplicacao desatualizada.
- A POC continua sem ferramenta automatica de validacao de links ou inventario operacional.

## Alternativas consideradas

1. **Manter somente o README**
   Pros: um unico documento para consulta.
   Contras: README ficaria grande demais e misturaria setup, arquitetura e detalhes operacionais.

2. **Manter somente ADRs**
   Pros: preserva historico de decisoes.
   Contras: ADRs explicam decisoes, mas nao substituem um inventario operacional atualizado.

3. **Criar varios documentos operacionais por tema**
   Pros: separacao mais granular.
   Contras: aumenta o custo de manutencao para uma POC e dispersa informacoes basicas.
