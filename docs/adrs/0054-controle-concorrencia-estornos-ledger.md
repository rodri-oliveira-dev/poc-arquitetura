# ADR-0054: Controle de concorrencia em estornos do LedgerService

## Status
Aceito

## Data
2026-05-07

## Contexto
A solicitacao assincrona de estorno verifica se ja existe solicitacao ativa para o lancamento original antes de inserir uma nova linha em `estornos_lancamentos`. Sem uma protecao no banco, duas requisicoes concorrentes com `Idempotency-Key` diferentes poderiam criar duas solicitacoes `Pending` ou `Processing` para o mesmo lancamento.

O processamento por worker tambem listava solicitacoes `Pending` sem claim atomico. Em execucoes concorrentes, mais de um worker poderia tentar processar a mesma solicitacao. A idempotencia financeira por `external_reference=estorno:{lancamentoOriginalId}` reduzia o risco de duplicar o lancamento compensatorio, mas nao impedia estados operacionais duplicados ou divergentes.

## Decisao
Adicionar um indice unico filtrado em `estornos_lancamentos(lancamento_original_id)` para status ativos:

- `Pending`;
- `Processing`.

O endpoint continua verificando a existencia de solicitacao ativa antes de inserir, mas o banco passa a ser a fonte final de protecao contra corrida. Quando a constraint e violada, a API traduz a falha para `409 Conflict`.

O worker passa a reclamar solicitacoes pendentes por claim atomico no PostgreSQL, usando `UPDATE ... WHERE status = 'Pending' ... FOR UPDATE SKIP LOCKED ... RETURNING`. A linha reclamada ja sai como `Processing`, evitando que outro worker selecione o mesmo estorno no mesmo ciclo.

O carregamento por id para processamento passa a usar `SELECT ... FOR UPDATE` no PostgreSQL. Assim, chamadas concorrentes diretas ao comando de processamento para o mesmo `estornoId` serializam a transicao e preservam a idempotencia do lancamento compensatorio e do Outbox final.

## Consequencias

### Beneficios
- Impede mais de uma solicitacao operacional ativa por lancamento original.
- Mantem erro coerente de conflito em corrida HTTP.
- Evita processamento simultaneo da mesma solicitacao por workers concorrentes.
- Preserva a protecao financeira existente por `external_reference` do compensatorio.
- Usa locks por linha e `SKIP LOCKED`, sem bloquear a tabela inteira.

### Trade-offs / custos
- A estrategia depende de recursos especificos do PostgreSQL.
- Testes de concorrencia relevantes passam a exigir PostgreSQL real via Testcontainers.
- Solicitacoes antigas duplicadas em status ativo precisariam ser saneadas antes de aplicar a migration em um ambiente com dados inconsistentes.

## Alternativas consideradas

1. Manter apenas checagem em Application.
   - Rejeitada porque nao protege contra corrida entre a leitura e a insercao.

2. Usar lock pessimista amplo por tabela.
   - Rejeitada porque aumentaria contencao sem necessidade; o escopo correto e por linha/chave.

3. Depender apenas da idempotencia financeira por `external_reference`.
   - Rejeitada porque ela protege o lancamento compensatorio, mas nao a previsibilidade operacional das solicitacoes de estorno.

4. Processar diretamente no request HTTP.
   - Rejeitada porque quebraria o fluxo assincrono definido nas ADRs 0049 e 0050.
