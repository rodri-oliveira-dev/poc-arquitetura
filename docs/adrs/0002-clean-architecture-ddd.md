# ADR-0002: Adotar Clean Architecture com DDD em cada microserviço

## Status
Aceito

## Data
2026-02-16

## Contexto
A PoC busca demonstrar Clean Architecture com foco em DDD, separando domínio, aplicação e infraestrutura.

## Decisão
Estruturar cada serviço em camadas:
- Api (controllers, middlewares, swagger)
- Application (casos de uso, validações)
- Domain (entidades e regras)
- Infrastructure (EF Core, Kafka, outbox/consumer)

## Consequências
- Reduz acoplamento a frameworks (domínio e aplicação ficam mais testáveis).
- Facilita troca de infraestrutura (ex.: outro broker, outro storage) com menos impacto.
- Introduz boilerplate e exige disciplina de dependências (direção correta das referências).

## Alternativas consideradas
- Arquitetura em camadas tradicional (Api/Services/Repo): mais simples, mas tende a acoplar domínio a EF/infra.
- Minimal vertical slice sem separação: bom para speed, pior para evolução e governança.
