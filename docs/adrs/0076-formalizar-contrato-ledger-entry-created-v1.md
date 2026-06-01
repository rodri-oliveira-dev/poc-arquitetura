# ADR-0076: Formalizar contrato LedgerEntryCreated.v1 e manter BRL como limitacao conhecida

## Status
Aceito

## Data
2026-06-01

## Contexto
O `LedgerService` publica `LedgerEntryCreated.v1` e o `BalanceService.Worker` o consome para atualizar `daily_balances`. A ADR-0017 definiu versionamento e DLQ, mas o payload ainda nao possuia schema validavel. O Balance aplicava `BRL` como default, enquanto o Ledger nao recebe nem persiste moeda.

## Decisao
- Formalizar o payload em JSON Schema Draft 2020-12 e manter exemplo valido em `docs/contracts/events`.
- Manter DTOs locais em cada servico, sem referencia direta de projeto apenas para compartilhar contrato.
- Rejeitar no consumer payloads com propriedades desconhecidas ou campos obrigatorios ausentes, encaminhando-os para DLQ.
- Nao adicionar `currency` em `LedgerEntryCreated.v1`.
- Documentar `BRL` como limitacao conhecida da POC.

## Racional sobre currency
Publicar `currency=BRL` agora nao criaria suporte real a moeda: o Ledger continuaria sem receber nem persistir esse dado. Tornar o campo obrigatorio tambem quebraria mensagens historicas `v1`. Uma evolucao para multiplas moedas deve tratar contrato HTTP, persistencia no Ledger, evento e leitura do Balance em conjunto.

## Consequencias
- O payload atual ganha fonte formal de verdade e exemplo validavel.
- Campos novos em `v1` exigem atualizacao coordenada de schema, produtor, consumer e testes.
- Suporte real a moeda permanece fora do escopo e deve usar nova decisao arquitetural.
