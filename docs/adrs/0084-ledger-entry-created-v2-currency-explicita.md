# ADR-0084: Criar LedgerEntryCreated.v2 com currency explicita

## Status
Aceito

## Data
2026-06-06

## Contexto
`LedgerEntryCreated.v1` nao carrega `currency`. O `BalanceService` aplicava `BRL` como fallback para atualizar `daily_balances`, o que deixava a semantica de moeda fora do contrato entre servicos.

Adicionar `currency` diretamente em v1 seria semanticamente relevante e poderia quebrar consumidores que validam propriedades conhecidas. Tambem haveria risco de mensagens antigas sem moeda serem tratadas como v1 incompletas.

## Decisao
- Criar `LedgerEntryCreated.v2` com `currency` explicito e obrigatorio no payload.
- Manter `LedgerEntryCreated.v1` como contrato legado de leitura.
- Fazer o `BalanceService.Worker` aceitar v1 e v2.
- Aplicar fallback `BRL` somente ao normalizar mensagens v1 legadas.
- Exigir `currency` em v2, sem fallback silencioso no caminho novo.
- Manter Pub/Sub e Kafka usando o mesmo payload logico, com diferencas de attributes e headers apenas nos adapters.
- Mapear `LedgerEntryCreated.v1` e `LedgerEntryCreated.v2` para o mesmo topico fisico da familia `ledger.ledgerentry.created`.

## Consequencias
- Novos eventos do Ledger passam a ser publicados como `LedgerEntryCreated.v2`.
- Mensagens antigas v1 continuam consumiveis durante a convivencia com Kafka legado ou backlog historico.
- O contrato v2 torna explicita a moeda usada pela POC atual.
- Suporte a multiplas moedas no contrato HTTP e na persistencia do Ledger continua fora desta decisao.
