# ADR-0009: Versionamento de API via URL segment (Asp.Versioning)

## Status
Aceito

## Data
2026-02-17

## Contexto
Como PoC de microserviços, a API evolui com o tempo. Precisamos de uma estratégia de versionamento que:

- seja explícita para o consumidor;
- funcione bem com Swagger;
- evite dependência de headers customizados.

O repositório já utiliza `Asp.Versioning` e as rotas mostram versões no caminho.

## Decisão
Adotar **URL segment versioning**:

- padrão: `api/v{version}/...` (Ledger)
- padrão: `/v{version}/...` (Balance)

O Swagger deve expor (quando aplicável) as versões disponíveis e agrupar endpoints por versão.

## Consequências

### Benefícios
- Descoberta fácil e explícita (a versão está na URL).
- Integração simples com gateways e caches.
- Facilita coexistência de versões durante migrações.

### Trade-offs / custos
- URLs mudam quando a versão muda (breaking change de URL).
- Pode gerar duplicação de controllers/DTOs se múltiplas versões precisarem coexistir.

## Alternativas consideradas

1) **Header-based** (ex.: `api-version: 1`)
   - Prós: URL não muda.
   - Contras: menos visível; pode complicar caches e alguns clientes.

2) **Query string** (ex.: `?api-version=1`)
   - Prós: simples.
   - Contras: menos “clean” e pode ser ignorado acidentalmente.
