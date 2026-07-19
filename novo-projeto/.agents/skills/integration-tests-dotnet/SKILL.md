---
name: integration-tests-dotnet
description: Use esta skill para criar ou revisar testes de integração .NET, incluindo WebApplicationFactory, EF Core, Testcontainers PostgreSQL, autenticação, isolamento e fixtures. Não use para testes unitários simples ou testes distribuídos sem necessidade.
---

# Objetivo

Criar testes de integração confiáveis, isolados e proporcionais ao risco, escolhendo o ambiente de teste pelo comportamento que precisa ser validado.

## Quando usar

- Endpoints HTTP e pipeline ASP.NET Core.
- DI completa, filtros, serialização e Problem Details.
- EF Core, migrations, constraints, transações e queries.
- Autenticação e autorização.
- Concorrência de agendamento.
- Fixtures, seeds e limpeza de dados.
- Hosted services quando fizerem parte do comportamento.

## Escolha do tipo de teste

### `WebApplicationFactory`

Use quando precisar validar:

- contrato HTTP;
- roteamento;
- middleware;
- autenticação;
- DI;
- serialização;
- comportamento integrado da aplicação.

### Provider leve ou substituto

Use somente quando SQL, constraints, transações, migrations e comportamento do provider não forem o alvo.

### Testcontainers PostgreSQL

Use quando a confiança depender de:

- SQL real;
- índices e constraints;
- concorrência;
- transações;
- migrations;
- tipos PostgreSQL;
- comportamento do Npgsql.

## Processo

1. Defina o risco real a validar.
2. Identifique se precisa de HTTP real, DI, banco real ou apenas composição interna.
3. Preserve a factory existente quando suficiente.
4. Desative integrações externas e hosted services que não fazem parte do cenário.
5. Use relógio controlado por `TimeProvider` em cenários temporais.
6. Mantenha seed e limpeza previsíveis.
7. Evite portas fixas e dependências externas não controladas.
8. Teste concorrência de forma determinística, sem sleeps arbitrários.
9. Verifique se o teste cobre o comportamento, não apenas o status code.
10. Documente mudança quando a estratégia oficial de testes for alterada.

## Cenários prioritários para o petshop

- dois usuários tentando reservar o mesmo horário;
- conflito de profissional ou recurso;
- reagendamento preservando invariantes;
- cancelamento liberando disponibilidade;
- idempotência de confirmação;
- validade de constraints únicas;
- consultas de agenda por período;
- autorização por unidade ou perfil;
- comportamento após expiração de reserva temporária.

## Validação

```bash
dotnet test ./tests/<Projeto>.csproj --configuration Release
```

Para Testcontainers, execute em ambiente com Docker compatível disponível.

## Restrições

- Não alterar testes para ocultar falha real.
- Não usar Compose, broker ou rede externa casualmente.
- Não introduzir `Thread.Sleep` ou atraso arbitrário.
- Não compartilhar estado mutável entre testes.
- Não usar banco de desenvolvimento pessoal.
- Não colocar segredo ou configuração local não documentada em fixtures.
