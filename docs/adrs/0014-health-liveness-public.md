# ADR-0014: Expor /health como liveness público

## Status
Aceito

## Data
2026-02-16

## Contexto
O README define `GET /health` retornando 200 com body `ok`, público, para liveness.

## Decisão
Manter endpoint simples e público para indicar processo “de pé”.

## Consequências
- Fácil integração com probes e monitoramento básico.
- Não indica dependências prontas (DB/Kafka). Isso é um gap para readiness (ver ADR-0101).

## Alternativas consideradas
- Health check completo no mesmo endpoint: pode causar flapping e confundir liveness vs readiness.
- Não expor health: dificulta operação e observabilidade.
