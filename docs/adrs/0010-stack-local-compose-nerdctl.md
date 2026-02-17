# ADR-0010: Stack local via compose (nerdctl) com Kafka e Postgres

## Status
Aceito

## Data
2026-02-17

## Contexto
Para validar a PoC ponta a ponta (Ledger -> Outbox -> Kafka -> Balance) é necessário subir dependências locais:

- Kafka
- Postgres (Ledger)
- Postgres (Balance)

Além dos próprios microserviços.

Precisamos de um setup reprodutível e com baixo atrito para executar em dev e em CI (quando aplicável).

## Decisão
Manter um `compose.yaml` na raiz do repositório para subir a stack completa com **nerdctl compose** (containerd), contendo:

- redes/volumes;
- serviços de banco;
- serviço Kafka (KRaft single node) com `auto.create.topics=false`;
- job de init para criação idempotente de tópicos;
- build dos microserviços via Dockerfiles do repo.

## Consequências

### Benefícios
- Execução local padronizada (`nerdctl compose up -d --build`).
- Facilita reproduzir problemas e rodar testes de carga (k6 dentro da rede do compose).
- Infra explicitada em código (YAML) e versionada.

### Trade-offs / custos
- Dependência em Docker/nerdctl e recursos de máquina.
- Mais moving parts; debugging pode exigir entendimento de rede/ports.
- Diferenças entre execução “host” e “compose” precisam ser documentadas (ex.: hosts de DB/Kafka).

## Alternativas consideradas

1) **Executar tudo no host** (sem containers)
   - Prós: menos camadas.
   - Contras: instalação/configuração manual; menos reprodutível.

2) **Kubernetes local** (kind/minikube)
   - Prós: aproxima produção.
   - Contras: pesado para a PoC; curva e manutenção.
