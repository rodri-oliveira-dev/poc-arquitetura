# Estudo 006 - Contratos de erro e status codes

## Leitura da Product Owner

### Contexto

As APIs possuem documentacao e OpenAPI versionado. Um estudo util e revisar se erros de validacao, conflito, autenticacao, autorizacao e falha operacional estao previsiveis para consumidores.

### Objetivo de negocio

Melhorar previsibilidade das APIs para clientes e testes automatizados.

### Historia de usuario

Como consumidor da API, quero respostas de erro consistentes para tratar falhas de forma previsivel.

### Criterios de aceite

- Levantar status codes documentados para Ledger e Balance.
- Comparar documentacao, OpenAPI e comportamento esperado.
- Registrar inconsistencias encontradas.
- Propor um padrao de erro por tipo de falha.
- Se houver ajuste pequeno e objetivo, implementar em PR separado ou no mesmo PR com justificativa.

## Leitura do Arquiteto

### Abordagem tecnica

Comecar por levantamento documental. Qualquer mudanca de comportamento deve ser pequena, testada e refletida nos contratos.

### Conceitos praticados

- ProblemDetails.
- Contratos HTTP.
- OpenAPI drift.
- Testes de contrato.
- Autenticacao e autorizacao.

### Escopo sugerido de PR

Primeiro PR documental. Segundo PR para uma correcao objetiva, se a analise apontar necessidade.

### Classificacao

Util, mas opcional. Necessario se houver inconsistencia real entre documentacao e comportamento.
