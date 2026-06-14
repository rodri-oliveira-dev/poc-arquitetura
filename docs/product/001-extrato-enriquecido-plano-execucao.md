# Plano de execucao - Extrato enriquecido

Este plano detalha a execucao da oportunidade de produto "Extrato enriquecido com saldo apos cada lancamento".

A funcionalidade deve ser entregue em fatias pequenas. Cada etapa inclui testes unitarios, testes de componente e testes de integracao quando fizer sentido.

## Objetivo de produto

Permitir que o usuario consulte um extrato financeiro por merchant e periodo, visualizando os lancamentos em ordem deterministica e o saldo apos cada lancamento.

## Prompt 1 - Descoberta tecnica e contrato

### Objetivo

Mapear o estado atual de Ledger e Balance e propor o contrato da nova consulta.

### Entregas

- Decidir em qual API o endpoint deve ficar.
- Definir query params minimos.
- Definir response model.
- Definir ordenacao deterministica.
- Definir comportamento sem movimentacao.
- Definir estrategia de paginacao.

### Testes

- Unitarios: nao aplicavel nesta etapa.
- Componente: nao aplicavel nesta etapa.
- Integracao: nao aplicavel nesta etapa.

### Validacao

A validacao e documental. O PR deve registrar contrato proposto, trade-offs e proximos passos.

## Prompt 2 - Modelo de resposta e regra de saldo acumulado

### Objetivo

Criar a regra de composicao do extrato na camada de Application, sem expor endpoint HTTP ainda.

### Entregas

- Request interno do caso de uso.
- Response interno do extrato.
- Item do extrato com valor, tipo, data, referencia e saldo apos lancamento.
- Regra de saldo acumulado em ordem deterministica.

### Testes unitarios

- Deve calcular saldo acumulado para lista de lancamentos positivos.
- Deve calcular saldo acumulado para entradas e saidas misturadas.
- Deve manter saldo correto quando houver estorno ou movimento negativo, conforme modelo atual.
- Deve retornar lista vazia e saldo adequado quando nao houver lancamentos.
- Deve respeitar a ordenacao recebida ou aplicar ordenacao explicitamente, conforme decisao do contrato.

### Testes de componente

- Caso de uso com repositorio fake ou stub deve retornar response completa.
- Caso de uso deve validar periodo invalido.
- Caso de uso deve validar merchant obrigatorio, se esse for o contrato escolhido.

### Testes de integracao

- Nao obrigatorio nesta etapa se a Infrastructure ainda nao foi alterada.

## Prompt 3 - Consulta de dados na Infrastructure

### Objetivo

Implementar a consulta real dos lancamentos necessarios para montar o extrato.

### Entregas

- Query EF Core com filtro por merchant.
- Filtro por periodo.
- Ordenacao deterministica.
- Paginacao.
- Projecao apenas dos campos necessarios.

### Testes unitarios

- Nao priorizar testes unitarios para query EF pura.
- Testar apenas helpers ou specifications se forem criados.

### Testes de componente

- Repositorio com banco local de teste deve aplicar filtro por merchant.
- Repositorio deve aplicar filtro por periodo.
- Repositorio deve respeitar paginacao.
- Repositorio deve respeitar ordenacao quando houver lancamentos com mesma data.

### Testes de integracao

- Teste com PostgreSQL via Testcontainers ou padrao ja usado no repositorio.
- Deve persistir lancamentos reais de teste e recuperar na ordem esperada.
- Deve validar comportamento com pagina vazia.
- Deve validar que outro merchant nao aparece no resultado.

## Prompt 4 - Endpoint HTTP

### Objetivo

Expor a consulta de extrato pela API escolhida.

### Entregas

- Endpoint HTTP.
- Binding de query params.
- Autorizacao coerente com a API atual.
- ProblemDetails para erros de entrada.
- Response documentado.

### Testes unitarios

- Unitarios apenas para validadores ou mappers, se existirem.

### Testes de componente

- Endpoint handler deve converter request HTTP em request de Application corretamente.
- Mapper de response deve preservar saldo apos lancamento.
- Validacao deve rejeitar periodo invalido.

### Testes de integracao

- WebApplicationFactory deve chamar endpoint com token ou autenticacao de teste, conforme padrao do projeto.
- Deve retornar 200 para consulta valida.
- Deve retornar 400 para periodo invalido.
- Deve retornar 401 ou 403 conforme regra de autenticacao/autorizacao existente.
- Deve retornar lista vazia quando nao houver lancamentos.

## Prompt 5 - Documentacao, OpenAPI e exemplos

### Objetivo

Fechar a funcionalidade como produto consumivel.

### Entregas

- Atualizar documentacao da API.
- Adicionar exemplo de request.
- Adicionar exemplo de response.
- Documentar ordenacao.
- Documentar pagina vazia.
- Atualizar OpenAPI se o fluxo do repositorio exigir.

### Testes unitarios

- Nao aplicavel.

### Testes de componente

- Nao aplicavel.

### Testes de integracao

- Executar testes existentes impactados.
- Executar validacao de OpenAPI, se aplicavel ao repositorio.

## Prompt 6 - Validacao final e hardening pequeno

### Objetivo

Revisar a funcionalidade completa e corrigir lacunas pequenas antes de considerar a entrega pronta.

### Entregas

- Revisao de performance da query.
- Revisao de nomes e contrato.
- Revisao de autorizacao.
- Revisao de documentacao.
- Ajustes pequenos encontrados pelos testes.

### Testes unitarios

- Garantir cobertura das regras de saldo acumulado e validacao.

### Testes de componente

- Garantir cobertura do caso de uso com diferentes combinacoes de entrada.

### Testes de integracao

- Rodar suite de integracao relacionada a Ledger e Balance.
- Rodar teste ponta a ponta se houver fluxo local preparado para criar lancamento e consultar extrato.

## Recomendacao de ordem

1. Contrato e decisao de API.
2. Regra de saldo acumulado com unitarios.
3. Query de Infrastructure com componente e integracao.
4. Endpoint HTTP com componente e integracao.
5. Documentacao e OpenAPI.
6. Hardening e validacao final.
