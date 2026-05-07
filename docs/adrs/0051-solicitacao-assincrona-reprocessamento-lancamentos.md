# ADR-0051: Solicitacao assincrona de reprocessamento de lancamentos

## Status
Aceito

## Data
2026-05-07

## Contexto
O `LedgerService.Api` ja possui criacao de lancamentos e fluxo de estorno assincrono com MediatR, idempotencia, persistencia transacional e Outbox. A nova operacao `POST /api/v1/lancamentos/reprocessar` precisa registrar uma demanda operacional de reprocessamento sem executar lote pesado durante o request HTTP.

O dominio atual do Ledger nao possui `contaId`; as operacoes sao segmentadas por `merchantId` e a autorizacao por merchant foi definida na ADR-0023. Por isso, o filtro de escopo do reprocessamento deve usar `merchantId` e periodo.

## Decisao
Criar uma solicitacao persistente de reprocessamento em `reprocessamentos_lancamentos`, com status inicial `Pending`, e expor:

- `POST /api/v1/lancamentos/reprocessar` para criar a solicitacao;
- `GET /api/v1/lancamentos/reprocessamentos/{reprocessamentoId}` para consultar o status registrado.

O endpoint de criacao segue o padrao do estorno:

- controller fino em `LedgerService.Api`;
- bind/map dedicado na API;
- command e handler via MediatR em `LedgerService.Application`;
- validacao por FluentValidation;
- autorizacao por merchant baseada no contexto recebido da borda HTTP;
- idempotencia por `merchantId` + `Idempotency-Key`;
- persistencia e Outbox na mesma unidade transacional;
- resposta `202 Accepted` com `Location` e `statusUrl`.

A solicitacao grava o evento operacional `ReprocessamentoLancamentosSolicitado.v1` no Outbox, mapeado para o topico `ledger.lancamentos.reprocessamento.solicitado`. Esse evento representa intencao de processamento, nao fato financeiro final.

O processamento efetivo do reprocessamento fica fora desta decisao. A tabela e o evento deixam um ponto de extensao para worker/background flow posterior, sem bloquear a requisicao HTTP.

## Consequencias

### Beneficios
- Mantem o endpoint HTTP sem processamento pesado sincrono.
- Reutiliza o padrao ja aceito para comandos MediatR, idempotencia e Outbox no Ledger.
- Mantem fronteiras entre API, Application, Domain e Infrastructure.
- Evita introduzir `contaId`, que nao existe no dominio atual.
- Permite replay estavel para chamadas repetidas com a mesma `Idempotency-Key`.
- Deixa rastreavel a demanda operacional por tabela, status e evento de intencao.

### Trade-offs / custos
- Introduz nova tabela, migration, repositorio e contrato de evento operacional.
- O campo `LedgerEntryId` da tabela de idempotencia e reutilizado para guardar o identificador da solicitacao, mantendo o mecanismo existente em vez de criar uma nova tabela de idempotencia.
- O topico operacional de reprocessamento existe antes de haver worker dedicado.
- Estados alem de `Pending` ficam modelados para compatibilidade futura, mas nao evoluem nesta tarefa.

## Alternativas consideradas

1. Processar os lancamentos dentro do endpoint HTTP.
   - Rejeitada porque aumentaria latencia, risco operacional e acoplamento da API.

2. Usar `contaId` no contrato.
   - Rejeitada porque `contaId` nao existe no modelo atual do Ledger e criaria conceito incompativel com a autorizacao por merchant.

3. Reutilizar a tabela de estornos.
   - Rejeitada porque reprocessamento e estorno sao intencoes operacionais diferentes, com filtros e evolucao independentes.

4. Publicar diretamente no Kafka a partir do controller.
   - Rejeitada porque violaria o padrao de Outbox transacional da ADR-0003.

5. Criar worker de reprocessamento nesta mesma tarefa.
   - Adiada para manter o escopo controlado: esta decisao entrega solicitacao, persistencia, idempotencia, status e intencao operacional.

## Impacto nos testes
- Testes unitarios cobrem validator, handler de criacao, idempotencia, autorizacao por merchant e query de status.
- Testes de integracao HTTP cobrem `202`, `400`, `401`, `403`, status, `Location`, persistencia e Outbox.
- O fluxo existente de lancamentos e estornos permanece coberto pelos testes de regressao ja existentes.

## Impacto operacional
- Aplicar a nova migration do `LedgerService`.
- Criar o topico `ledger.lancamentos.reprocessamento.solicitado` no Kafka local ou no ambiente alvo.
- Monitorar solicitacoes `Pending` em `reprocessamentos_lancamentos` enquanto o worker de reprocessamento nao existir.
