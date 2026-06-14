# Oportunidades de produto

Este backlog reposiciona os estudos para uma visao de produto. O sistema registra lancamentos financeiros, publica eventos e mantem uma projecao de saldo para consulta. A arquitetura deve apoiar a execucao, mas nao ser o objetivo principal.

## Leitura de mercado resumida

Produtos financeiros com historico de transacoes costumam valorizar consulta rica, categorizacao, acompanhamento de mudancas, recorrencia, conciliacao e previsao de fluxo. Para o laboratorio, isso vira um backlog de funcionalidades locais, sem dependencia de cloud real.

## Priorizacao inicial

| Prioridade | Funcionalidade | Valor de produto | Primeiro PR sugerido |
| --- | --- | --- | --- |
| 1 | Extrato enriquecido com saldo apos cada lancamento | Ajuda o usuario a explicar a evolucao do saldo | Endpoint de consulta com running balance |
| 2 | Categorias e tags de lancamento | Facilita analise, filtro e relatorios | Modelo simples de categoria no lancamento |
| 3 | Status do lancamento | Diferencia pendente, efetivado, estornado e rejeitado | Campo de status e documentacao de ciclo de vida |
| 4 | Lancamentos recorrentes | Permite simular mensalidades, tarifas e receitas recorrentes | Cadastro de recorrencia e geracao manual local |
| 5 | Projecao de fluxo de caixa | Mostra saldo futuro esperado com base em recorrencias | Consulta de saldo projetado por periodo |
| 6 | Conciliacao basica | Compara lancamentos esperados e realizados | Relatorio de divergencias simples |
| 7 | Alertas de saldo e volume | Ajuda a detectar comportamento fora do esperado | Regras locais de alerta sem notificacao externa |
| 8 | Aprovacao para ajustes manuais | Reduz risco em correcoes sensiveis | Fluxo maker-checker simples para ajustes |

---

## 1. Extrato enriquecido com saldo apos cada lancamento

### Problema do usuario

Hoje o usuario pode consultar saldo, mas precisa de uma visao explicativa da evolucao do saldo ao longo dos lancamentos.

### Proposta

Criar uma consulta de extrato por merchant, periodo e filtros basicos, retornando os lancamentos em ordem e o saldo apos cada item.

### Historia

Como usuario financeiro, quero consultar um extrato com saldo apos cada lancamento para entender como o saldo final foi formado.

### Criterios de aceite

- Permitir filtro por merchant e periodo.
- Ordenar os lancamentos de forma deterministica.
- Retornar valor, tipo, data, referencia e saldo apos o lancamento.
- Documentar comportamento quando nao houver movimentacao.

### Plano arquitetural

- Api: novo endpoint de consulta.
- Application: caso de uso de extrato.
- Infrastructure: query paginada e ordenada.
- Tests: integracao cobrindo ordenacao e calculo do saldo acumulado.

---

## 2. Categorias e tags de lancamento

### Problema do usuario

Lancamentos sem classificacao sao dificeis de analisar por finalidade, origem ou tipo de gasto/receita.

### Proposta

Permitir categorias controladas e tags livres nos lancamentos.

### Historia

Como usuario financeiro, quero classificar lancamentos para consultar e agrupar movimentacoes por finalidade.

### Criterios de aceite

- Permitir categoria opcional no lancamento.
- Permitir tags opcionais com limite de quantidade e tamanho.
- Permitir filtro por categoria.
- Documentar regras de validacao.

### Plano arquitetural

- Domain: avaliar se categoria tem regra propria ou se comeca como atributo simples.
- Application: validar entrada.
- Infrastructure: mapping e indices para filtro.
- Tests: validacao e consulta por categoria.

---

## 3. Status do lancamento

### Problema do usuario

Nem todo movimento financeiro nasce efetivado. Produtos financeiros frequentemente precisam distinguir movimentos pendentes, confirmados, estornados ou rejeitados.

### Proposta

Evoluir o lancamento para ter ciclo de vida explicito.

### Historia

Como usuario financeiro, quero saber o status de um lancamento para diferenciar saldo confirmado de movimento ainda pendente.

### Criterios de aceite

- Definir status inicial permitido.
- Permitir transicao controlada entre status.
- Manter compatibilidade com estorno existente.
- Documentar impacto no saldo projetado.

### Plano arquitetural

- Domain: modelar transicoes validas.
- Application: comandos separados para criar e confirmar quando necessario.
- Events: avaliar se mudanca de status gera evento.
- Tests: transicoes validas e invalidas.

---

## 4. Lancamentos recorrentes

### Problema do usuario

Muitos lancamentos reais sao recorrentes, como mensalidades, tarifas, assinaturas, juros e repasses.

### Proposta

Cadastrar uma regra de recorrencia e gerar lancamentos previstos de forma manual no laboratorio.

### Historia

Como usuario financeiro, quero registrar uma recorrencia para nao criar manualmente lancamentos repetitivos.

### Criterios de aceite

- Permitir recorrencia mensal simples.
- Definir data inicial, valor, categoria e limite de ocorrencias.
- Gerar ocorrencias em modo controlado.
- Evitar duplicidade por chave de recorrencia e competencia.

### Plano arquitetural

- Domain: regra de recorrencia pequena.
- Application: caso de uso para gerar ocorrencias.
- Infrastructure: persistencia da regra e idempotencia.
- Tests: geracao, limite e duplicidade.

---

## 5. Projecao de fluxo de caixa

### Problema do usuario

Saldo atual nao responde se o saldo futuro sera suficiente diante de lancamentos previstos.

### Proposta

Criar consulta de saldo projetado por periodo usando lancamentos realizados e recorrencias previstas.

### Historia

Como usuario financeiro, quero visualizar saldo futuro esperado para antecipar risco de falta de saldo.

### Criterios de aceite

- Consultar saldo projetado por merchant e periodo.
- Separar realizado de previsto.
- Informar saldo inicial, entradas previstas, saidas previstas e saldo final projetado.
- Documentar que e uma projecao, nao saldo confirmado.

### Plano arquitetural

- Application: caso de uso de consulta.
- Infrastructure: composicao de realizados e previstos.
- Tests: cenarios com e sem recorrencia.

---

## 6. Conciliacao basica

### Problema do usuario

Quando existe diferenca entre o esperado e o realizado, o usuario precisa localizar a divergencia rapidamente.

### Proposta

Criar um relatorio simples que compare lancamentos esperados com lancamentos realizados.

### Historia

Como usuario financeiro, quero identificar divergencias entre esperado e realizado para tomar acao manual.

### Criterios de aceite

- Comparar por merchant, periodo, valor e referencia externa.
- Classificar divergencias como ausente, duplicado ou valor diferente.
- Nao corrigir automaticamente.
- Retornar resumo e detalhes.

### Plano arquitetural

- Application: comparacao deterministica.
- Infrastructure: consultas por referencia e periodo.
- Tests: ausente, duplicado e valor divergente.

---

## 7. Alertas de saldo e volume

### Problema do usuario

O usuario pode querer saber quando saldo ou volume passam de limites relevantes.

### Proposta

Permitir regras locais de alerta consultaveis, sem notificacao externa nesta fase.

### Historia

Como usuario financeiro, quero configurar limites para identificar saldos negativos ou volumes atipicos.

### Criterios de aceite

- Configurar limite minimo de saldo por merchant.
- Configurar limite de volume por periodo.
- Consultar alertas gerados.
- Documentar que nao ha envio externo de notificacao nesta fase.

### Plano arquitetural

- Domain/Application: regras simples de avaliacao.
- Infrastructure: persistencia das regras e consulta dos alertas.
- Tests: limite atingido e nao atingido.

---

## 8. Aprovacao para ajustes manuais

### Problema do usuario

Ajustes manuais podem corrigir problemas, mas tambem criam risco operacional se forem aplicados sem revisao.

### Proposta

Criar fluxo simples de solicitacao e aprovacao de ajuste manual.

### Historia

Como responsavel financeiro, quero revisar ajustes manuais antes que afetem o saldo.

### Criterios de aceite

- Criar solicitacao de ajuste com justificativa.
- Aprovar ou rejeitar a solicitacao.
- Aplicar lancamento apenas apos aprovacao.
- Manter historico da decisao.

### Plano arquitetural

- Domain: estado da solicitacao de ajuste.
- Application: comandos de solicitar, aprovar e rejeitar.
- Infrastructure: persistencia e idempotencia.
- Tests: fluxo aprovado, rejeitado e tentativa invalida.
