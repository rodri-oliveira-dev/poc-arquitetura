# Estudo 002 - Relatorio local de divergencia da projecao

## Leitura da Product Owner

### Contexto

A documentacao operacional ja trata recuperacao de eventos e rebuild de projecao. Uma fatia segura para estudo e produzir um relatorio local de divergencia, sem alterar saldo projetado automaticamente.

### Objetivo de negocio

Permitir diagnostico local de possiveis inconsistencias antes de qualquer decisao de correcao ou reprocessamento.

### Historia de usuario

Como mantenedor do laboratorio, quero solicitar um relatorio de divergencia da projecao para entender se o saldo calculado continua coerente com os dados de origem.

### Criterios de aceite

- Definir uma entrada local para solicitar o relatorio.
- O fluxo deve ser apenas diagnostico.
- O resultado deve informar se ha divergencias e apresentar um resumo analisavel.
- Deve haver filtro suficiente para evitar execucao ampla sem intencao.
- A documentacao da API ou do fluxo local deve ser atualizada.

## Leitura do Arquiteto

### Abordagem tecnica

Comecar pela menor superficie possivel. A regra principal e separar diagnostico de correcao. A implementacao deve reaproveitar casos de uso existentes quando isso fizer sentido, mantendo regras fora da camada HTTP.

### Fronteiras afetadas

- Api: contrato de entrada e resposta, se houver endpoint.
- Application: orquestracao do diagnostico.
- Infrastructure: consultas necessarias.
- Tests: unidade para o caso de uso e integracao para a entrada escolhida.
- Docs: contrato ou runbook atualizado.

### Conceitos praticados

- CQRS.
- Operacao local segura.
- Consultas orientadas a diagnostico.
- Testes de integracao.
- ProblemDetails para erros de entrada.

### Escopo sugerido de PR

Implementar somente o relatorio. Nao misturar com correcao automatica, rebuild destrutivo ou outros fluxos operacionais.

### Classificacao

Util, mas opcional.
