# ADR-0013: Health endpoint público de liveness (`GET /health`)

## Status
Aceito

## Data
2026-02-17

## Contexto
Os serviços expõem autenticação obrigatória por default (fallback policy). Ainda assim, precisamos de um endpoint simples para:

- checagem de vida (liveness) em ambientes containerizados;
- troubleshooting local;
- integração com ferramentas de monitoração.

Além disso, esse endpoint não deve depender de DB/Kafka, para não introduzir cascata e nem confundir liveness com readiness.

## Decisão
Implementar `GET /health` em todos os microserviços de API:

- público (`[AllowAnonymous]`), pois é usado por health checks externos;
- retorna `200` com body `ok` e `Content-Type: text/plain`;
- não sofre rate limiting (se houver), para não impactar checks automatizados.

Readiness (verificação de dependências) fica como melhoria futura separada.

## Consequências

### Benefícios
- Endpoint padronizado e simples.
- Facilita automação e operação.
- Não depende de infraestrutura externa para indicar “processo está vivo”.

### Trade-offs / custos
- Pode dar falsa sensação de “tudo OK” se dependências estiverem fora (por isso é liveness, não readiness).
- Se exposto publicamente sem controle de rede, pode virar ruído (embora a resposta seja inofensiva).

## Alternativas consideradas

1) **Health check autenticado**
   - Prós: reduz superfície.
   - Contras: quebra ferramentas/ambientes que precisam checar sem token; adiciona complexidade.

2) **Um único health global** (gateway)
   - Prós: centraliza.
   - Contras: esconde problema por serviço; gateway vira dependência.
