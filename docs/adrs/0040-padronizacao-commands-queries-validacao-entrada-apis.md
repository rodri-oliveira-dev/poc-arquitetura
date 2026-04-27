# ADR-0040: Padronizacao de Commands, Queries e Validacao de Entrada nas APIs

## Status
Aceito

## Data
2026-04-27

## Contexto
`LedgerService.Api` e `BalanceService.Api` expõem contratos HTTP sobre casos de uso de escrita e leitura.

O `BalanceService` ja usa MediatR com `Commands`, `Queries`, handlers e `ValidationBehavior` na camada Application. O `LedgerService` possui uma unica operacao HTTP de escrita e usa um input de aplicacao validado por FluentValidation e orquestrado por `CreateLancamentoService`, preservando controllers magros e fronteiras de Clean Architecture.

Durante a revisao, foi identificada uma divergencia no contrato de entrada do `LedgerService.Api`: o campo monetario `amount` era representado como `double` no DTO HTTP, enquanto dominio e persistencia usam `decimal` com precisao `18,2`.

## Decisao
Manter a organizacao existente quando ela ja for consistente:

- `BalanceService.Api` envia consultas por `ISender` para queries na camada Application.
- `BalanceService.Application` mantem queries, command de consumo de evento e validators registrados no pipeline MediatR.
- `LedgerService.Api` continua usando bind/map dedicado e delegacao para `CreateLancamentoService`, sem introduzir MediatR artificialmente para uma unica operacao de escrita.
- Valores monetarios de entrada devem usar `decimal`, nunca `double` ou `float`.
- Validacao de entrada com FluentValidation deve proteger contrato e formatos simples antes de acionar handlers/services.
- Invariantes de dominio continuam protegidas nas entidades de dominio.

O ajuste implementado altera `CreateLancamentoRequest.Amount` de `double` para `decimal` e adiciona validacao de precisao/escala compativel com `decimal(18,2)`.

## Consequencias

### Beneficios
- Evita perda de precisao em valores monetarios no contrato HTTP do Ledger.
- Alinha entrada, Application, dominio e persistencia para `amount`.
- Mantem a separacao entre validacao de contrato e invariantes de dominio.
- Evita uma migracao arquitetural desnecessaria para MediatR no Ledger.

### Trade-offs / custos
- Consumidores que dependam do schema OpenAPI podem perceber mudanca de formato numerico de `double` para `decimal`.
- A API passa a rejeitar valores com mais de duas casas decimais antes de chegar ao dominio.
- O Ledger permanece com uma abordagem equivalente a command/service, mas sem nomes formais `Command`/`Handler`.

## Alternativas consideradas

1. Introduzir MediatR no `LedgerService` agora.
   - Rejeitada porque criaria mais estrutura para uma unica operacao e nao corrigiria o problema monetario por si so.

2. Manter `double` no DTO HTTP e converter para `decimal` depois.
   - Rejeitada porque preservaria risco de precisao em dinheiro na borda publica.

3. Duplicar todas as invariantes de dominio no FluentValidation.
   - Rejeitada porque FluentValidation deve validar entrada e contrato, enquanto o dominio continua responsavel por invariantes.

## Impacto em LedgerService.Api
- `POST /api/v1/lancamentos` passa a modelar `amount` como `decimal`.
- `amount` deve respeitar ate 18 digitos totais e ate 2 casas decimais.
- A regra publica de sinal permanece a mesma: `CREDIT > 0`, `DEBIT < 0`, zero invalido.
- Rotas, headers, resposta de sucesso e autorizacao nao mudam.

## Impacto em BalanceService.Api
- Nenhuma mudanca de contrato HTTP.
- As queries de leitura continuam com MediatR e validators existentes.
- O limite de periodo permanece configurado por `ApiLimits:MaxBalancePeriodDays`.

## Politica para Commands
- Operacoes de escrita devem representar intencao de negocio na camada Application.
- Commands ou inputs equivalentes nao devem carregar campos que possam ser derivados de token, rota, header ou contexto quando isso for aplicavel.
- Commands nao devem retornar modelos de consulta alem do necessario para o contrato da operacao.

## Politica para Queries
- Operacoes de leitura devem usar queries na camada Application quando houver MediatR no servico.
- Queries nao devem causar efeitos colaterais.
- Filtros, datas e intervalos devem ser validados antes de executar acesso a dados.
- Parametros de merchant recebidos no contrato HTTP devem ser confrontados com a claim de autorizacao antes da consulta.

## Politica para FluentValidation
- Validar campos obrigatorios, formatos, limites de tamanho, ranges simples e restricoes publicas do contrato.
- Validar valores monetarios com `decimal` e escala compativel com persistencia.
- Nao mover regra de negocio complexa para validators.
- Manter o dominio como ultima linha de defesa para invariantes.

## Relacao entre validacao de entrada e invariantes de dominio
FluentValidation rejeita entradas invalidas de forma previsivel para consumidores HTTP. As entidades de dominio continuam rejeitando estados invalidos mesmo quando chamadas por outros fluxos internos, testes, consumidores Kafka ou futuras interfaces.

## Impacto em scripts e load tests
Foram revisados os scripts em `scripts` e os cenarios em `loadtests/k6/scenarios`.

Nenhum ajuste foi necessario porque os cenarios existentes enviam `amount` numerico valido e nao dependem de mais de duas casas decimais, rotas novas, headers novos ou novo formato de resposta.

## Proximos passos
- Avaliar introducao de MediatR no `LedgerService` somente se novas operacoes de escrita/leitura aumentarem a complexidade de orquestracao.
- Manter novos contratos monetarios sempre em `decimal`.
- Expandir validators de entrada quando novas restricoes publicas forem adicionadas ao dominio ou aos contratos HTTP.
