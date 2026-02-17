# ADR-0011: Stack local via nerdctl compose (containerd) e overrides por variáveis

## Status
Aceito

## Data
2026-02-16

## Contexto
O README define execução local via `nerdctl compose` com Postgres (2 instâncias), Kafka e init de tópico, e override de appsettings via env vars dentro da rede do compose.

## Decisão
- Manter `compose.yaml` compatível com nerdctl compose
- Expor portas para execução híbrida (app no host, infra em container)
- Usar `ConnectionStrings__DefaultConnection` e `Kafka__...__BootstrapServers` por env vars no compose

## Consequências
- Reprodutibilidade do ambiente local e menor fricção de setup.
- Evita hardcode de hostnames (127.0.0.1 vs nome do serviço).
- Exige documentação clara de portas e variáveis (o README já cobre bem).

## Alternativas consideradas
- docker compose: mais comum, mas o ambiente alvo aqui usa containerd/nerdctl.
- Kubernetes local: mais fiel ao prod, porém pesado para PoC.
