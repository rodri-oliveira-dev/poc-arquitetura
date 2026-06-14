# Plano de execucao - Extrato enriquecido

Este plano detalha a execucao da oportunidade de produto "Extrato enriquecido com saldo apos cada lancamento".

A funcionalidade deve ser entregue em fatias pequenas. Cada etapa inclui testes unitarios, testes de componente e testes de integracao quando fizer sentido.

## Objetivo de produto

Permitir que o usuario consulte um extrato financeiro por merchant e periodo, visualizando os lancamentos em ordem deterministica e o saldo apos cada lancamento.

## Regra central que os testes devem proteger

O saldo apos cada lancamento deve ser calculado a partir de uma sequencia deterministica de movimentos do mesmo merchant e periodo.

A query de Infrastructure e responsavel por entregar os movimentos corretos, filtrados e ordenados. A Application e responsavel por compor o extrato e calcular o saldo acumulado sem depender de detalhe de banco ou HTTP.

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

Criar a regra de composicao do extrato na camada de Application, sem expor endpoint HTTP e sem acoplar a regra ao EF Core.

### Entregas

- Request interno do caso de uso.
- Response interno do extrato.
- Item do extrato com valor, tipo, data, referencia e saldo apos lancamento.
- Regra pura de saldo acumulado.
- Validacoes de entrada do caso de uso.

### Regras que devem ser validadas

- O extrato pertence a um unico merchant.
- O periodo informado deve ser valido.
- A sequencia de calculo deve ser deterministica.
- O saldo apos lancamento deve refletir todos os movimentos anteriores da mesma sequencia.
- O calculo nao deve depender de API, banco, timezone local da maquina ou ordem acidental de colecao.
- Lista vazia deve ser um resultado valido, nao um erro.

### Testes unitarios obrigatorios

#### Calculo de saldo acumulado

- Dado saldo inicial zero e tres entradas positivas, deve acumular progressivamente.
- Dado saldo inicial zero com entrada, saida e nova entrada, deve refletir cada movimento na ordem.
- Dado saldo inicial diferente de zero, deve iniciar o acumulado a partir desse saldo.
- Dado movimento negativo ou estorno conforme modelo atual, deve reduzir o saldo corretamente.
- Dado valor decimal, deve preservar precisao monetaria sem arredondamento inesperado.

#### Ordenacao e determinismo

- Dado movimentos fora de ordem, deve calcular usando a ordenacao definida no contrato.
- Dado dois movimentos na mesma data/hora, deve usar criterio secundario estavel.
- Dado dois movimentos com mesma data/hora e mesmo criterio secundario invalido ou ausente, deve haver decisao explicita: rejeitar, ordenar por outro campo ou documentar impossibilidade.

#### Saida vazia

- Dado periodo sem movimentos, deve retornar lista vazia.
- Dado periodo sem movimentos e saldo inicial conhecido, deve retornar metadados coerentes, se o contrato incluir saldo inicial/final.

#### Validacao de entrada

- Merchant vazio deve falhar, se merchant for obrigatorio.
- Data inicial maior que data final deve falhar.
- Periodo aberto deve seguir decisao do contrato: rejeitar ou aplicar default documentado.
- Tamanho de pagina invalido deve falhar, se a paginacao for definida nesta etapa.

### Testes de componente obrigatorios

- Caso de uso com repositorio fake deve retornar extrato completo para uma sequencia simples.
- Caso de uso com repositorio fake deve propagar filtros de merchant e periodo para a porta de leitura.
- Caso de uso deve retornar erro de validacao sem consultar repositorio quando o request for invalido.
- Caso de uso deve montar response sem expor entidade de persistencia.
- Caso de uso deve manter o mesmo resultado quando recebe a mesma entrada em execucoes repetidas.

### Testes de integracao

Nao obrigatorios nesta etapa. A regra de saldo acumulado deve estar protegida antes de conectar EF Core e banco.

### Fora de escopo deste prompt

- Endpoint HTTP.
- Query EF Core real.
- Migration.
- OpenAPI.
- Otimizacao de indice.

## Prompt 3 - Consulta de dados na Infrastructure

### Objetivo

Implementar a consulta real dos lancamentos necessarios para montar o extrato, garantindo que a Infrastructure entregue para a Application exatamente a sequencia que a regra de saldo acumulado precisa.

### Entregas

- Query EF Core com filtro por merchant.
- Filtro por periodo.
- Ordenacao deterministica.
- Paginacao.
- Projecao apenas dos campos necessarios.
- Porta de leitura ou implementacao equivalente usada pela Application.

### Regras que devem ser validadas

- A query nao pode misturar movimentos de merchants diferentes.
- A query nao pode retornar movimentos fora do periodo contratado.
- A query deve retornar movimentos em ordem deterministica.
- A paginacao deve ser estavel para a mesma base de dados e os mesmos filtros.
- A projecao deve trazer todos os campos exigidos pela Application para calcular e exibir o extrato.
- A query nao deve depender de Include desnecessario nem carregar entidade completa se uma projecao simples resolver.

### Testes unitarios

Nao priorizar teste unitario para query EF Core pura.

Criar teste unitario apenas se houver:

- objeto de filtro com normalizacao de periodo;
- specification reutilizavel;
- builder de ordenacao;
- mapper de entidade para item de leitura.

### Testes de componente obrigatorios

Usar o padrao de banco de teste ja aceito no repositorio para validar a implementacao da porta de leitura.

#### Filtros

- Deve retornar apenas movimentos do merchant solicitado.
- Deve excluir movimentos de outro merchant no mesmo periodo.
- Deve incluir movimento exatamente na data inicial quando o contrato for inclusivo.
- Deve excluir ou incluir movimento exatamente na data final conforme decisao explicita do contrato.
- Deve retornar vazio quando nao houver dados para o periodo.

#### Ordenacao

- Deve ordenar por data do movimento.
- Deve aplicar criterio secundario estavel quando houver mesma data/hora.
- Deve manter a mesma ordem em execucoes repetidas com a mesma massa.

#### Paginacao

- Primeira pagina deve respeitar tamanho informado.
- Segunda pagina nao deve repetir itens da primeira.
- Paginacao deve preservar ordenacao global.
- Tamanho de pagina acima do limite deve ser rejeitado ou normalizado, conforme contrato.

#### Projecao

- Deve retornar valor, tipo, data, referencia e identificador necessario para desempate.
- Deve nao carregar dados que nao sao usados no extrato, quando isso puder ser validado de forma simples.

### Testes de integracao obrigatorios

Usar PostgreSQL via Testcontainers ou o padrao de integracao existente no repositorio.

- Persistir movimentos reais e recuperar apenas os do merchant correto.
- Persistir movimentos antes, dentro e depois do periodo e validar o recorte.
- Persistir movimentos com mesma data/hora e validar desempate deterministico.
- Persistir quantidade maior que uma pagina e validar pagina 1 e pagina 2.
- Validar que pagina vazia retorna sucesso com lista vazia.
- Validar que a query entrega dados suficientes para a Application calcular saldo apos lancamento.

### Teste de integracao entre Prompt 2 e Prompt 3

Depois da query real existir, adicionar pelo menos um teste integrando Application e Infrastructure:

- Dado movimentos persistidos fora de ordem, quando o caso de uso consultar o extrato, entao o retorno deve vir ordenado e com saldo apos cada lancamento correto.
- Dado movimentos de dois merchants, quando consultar um merchant, entao o saldo acumulado deve ignorar completamente o outro merchant.
- Dado pagina 2, quando consultar o extrato paginado, entao o comportamento de saldo deve seguir a decisao do contrato: saldo acumulado da pagina ou saldo acumulado global ate cada item.

### Decisao obrigatoria antes de implementar paginacao

Definir se `saldoAposLancamento` em uma pagina representa:

1. saldo acumulado global ate aquele lancamento, considerando movimentos anteriores fora da pagina; ou
2. saldo acumulado apenas dentro da pagina retornada.

Para produto financeiro, a recomendacao e usar saldo acumulado global, porque o usuario espera que o saldo apos o lancamento explique o saldo real naquele ponto da linha do tempo.

### Fora de escopo deste prompt

- Endpoint HTTP.
- Documentacao final de API.
- Alertas, conciliacao, recorrencia ou novas regras de produto.

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

- WebApplicationFactory deve chamar endpoint com autenticacao de teste, conforme padrao do projeto.
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
