# ADR-0013: (Ponto de melhoria) Readiness com verificação de DB e Kafka

## Status
Proposto

## Data
2026-02-17

## Contexto
Hoje existe `GET /health` como liveness simples. Porém, em ambientes reais, é comum separar:

- **liveness**: “processo está vivo” (não depende de infra);
- **readiness**: “processo consegue atender tráfego” (depende de DB/Kafka, timeouts, etc.).

Sem readiness, um orchestrator pode rotear tráfego para uma instância que está viva, mas sem conectividade com banco ou Kafka.

## Decisão
Planejar a criação de um endpoint de **readiness** separado (ex.: `GET /ready`), que valide de forma **rápida** e com **timeouts**:

- conectividade com o banco (`SELECT 1`);
- conectividade com Kafka (metadata/produce test opcional); 
- opcionalmente, dependências internas críticas.

O endpoint deve retornar:

- `200` quando pronto;
- `503` quando não pronto.

Não implementar automaticamente nesta etapa (registrar como melhoria).

## Consequências

### Benefícios
- Melhor governança operacional (tráfego só vai para instâncias prontas).
- Diagnóstico mais rápido (saber se o problema é infra ou aplicação).

### Trade-offs / custos
- Implementação deve ser cuidadosa para não causar cascata (timeouts curtos, sem chamadas pesadas).
- Pode precisar de política de autenticação/rede (geralmente interno do cluster).

## Alternativas consideradas

1) **Usar apenas `/health`**
   - Prós: simples.
   - Contras: não diferencia “vivo” de “pronto”.
