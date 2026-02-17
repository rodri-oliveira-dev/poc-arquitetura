# ADR-0002: Clean Architecture + DDD por microserviço

## Status
Aceito

## Data
2026-02-17

## Contexto
O repositório tem a intenção explícita de ser uma PoC que demonstre:

- separação de camadas e dependências direcionadas para dentro;
- domínio com regras explícitas (ex.: validações/constraints no `Domain`);
- infraestrutura (EF Core, Kafka) isolada e “plugável”.

Além disso, há múltiplos microserviços (Auth, Ledger, Balance) e precisamos evitar que a complexidade “vaze” entre camadas.

## Decisão
Organizar cada microserviço seguindo uma variação de **Clean Architecture**, com projetos separados:

- `*.Domain`: entidades, invariantes, exceções e contratos de repositório (sem dependências de infraestrutura).
- `*.Application`: casos de uso/serviços, validações e orquestração (dependendo do domínio).
- `*.Infrastructure`: EF Core, repositórios concretos, mensageria (Kafka), outbox, etc. (dependendo de Application/Domain).
- `*.Api`: camada de entrega HTTP (Controllers/Minimal API), composição/DI, middlewares e Swagger.

Regra-chave: **dependências apontam para dentro** (Api -> Infrastructure -> Application -> Domain), e o `Domain` não depende de nada “de fora”.

## Consequências

### Benefícios
- Domínio mais testável e com menos acoplamento a frameworks.
- Trocas de infraestrutura (ex.: mecanismo de mensageria) ficam mais localizadas.
- Facilita disciplina de boundaries em um repo com múltiplos serviços.

### Trade-offs / custos
- Mais projetos/arquivos e overhead de DI.
- Risco de “camadas vazias” em PoCs (arquitetura maior do que a necessidade).
- Exige padronização e revisão para evitar violação de boundaries (ex.: Api acessando DbContext direto).

## Alternativas consideradas

1) **Projeto único por microserviço** (Api + Domain + Infra misturado)
   - Prós: menos boilerplate.
   - Contras: acoplamento, testes mais difíceis, maior risco de “dependência do EF no domínio”.

2) **Arquitetura em pastas** (sem múltiplos csprojs)
   - Prós: menos projetos.
   - Contras: boundary mais fácil de quebrar; dependências ficam implícitas.
