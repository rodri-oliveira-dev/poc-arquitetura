# ADR-0049: Solicitacao assincrona de estorno de lancamento via Mediator

## Status
Aceito

## Data
2026-05-06

## Contexto
O `LedgerService.Api` ja possuia criacao de lancamentos com controller magro, validacao por FluentValidation, idempotencia e Outbox. A criacao ainda era orquestrada por `CreateLancamentoService`, conforme ADR-0040, porque havia uma unica operacao de escrita no Ledger.

A inclusao de `POST /api/v1/lancamentos/{lancamentoId}/estornos` adiciona uma segunda intencao de escrita. O novo caso de uso precisa:

- receber parametros de rota, body e headers HTTP;
- validar entrada;
- carregar o lancamento original;
- respeitar autorizacao por merchant;
- prevenir solicitacao ativa duplicada;
- persistir a solicitacao com status inicial `Pending`;
- registrar evento no Outbox;
- retornar `202 Accepted` sem processar o estorno financeiro no request.

## Decisao
Adotar MediatR no `LedgerService.Application` para novos casos de uso de escrita que representem comandos de negocio, com pipeline de validacao por FluentValidation.

O endpoint de estorno passa a:

- montar `SolicitarEstornoLancamentoCommand` na camada `Api`;
- enviar o comando via `ISender`;
- traduzir o resultado para o contrato HTTP.

O handler em `Application` concentra a orquestracao do caso de uso e usa portas de dominio para persistencia, idempotencia e Outbox. A camada `Infrastructure` implementa a persistencia de `EstornoLancamento` e a configuracao EF Core.

A autorizacao por merchant continua baseada em claims na borda HTTP. Como a rota recebe apenas o `lancamentoId`, a API extrai os merchants autorizados do token e o command carrega esse contexto de autorizacao de forma explicita, sem acoplar `Application` a `ClaimsPrincipal` ou ASP.NET Core.

O evento `LancamentoEstornoSolicitado.v1` e gravado no Outbox e mapeado para o topico `ledger.lancamento.estorno.solicitado`. Nao foi implementado consumidor de estorno nesta decisao.

O endpoint `GET /api/v1/lancamentos/estornos/{estornoId}` reutiliza a mesma decisao arquitetural para leitura: a camada `Api` monta `ObterStatusEstornoLancamentoQuery`, envia via `ISender` e traduz o resultado para contrato HTTP. O handler em `Application` consulta a porta `IEstornoLancamentoRepository`, aplica a autorizacao por merchant com o contexto recebido da borda HTTP e retorna um result publico, sem expor entidade de dominio ou detalhes EF Core.

## Consequencias

### Beneficios
- Formaliza MediatR no Ledger quando a quantidade de operacoes de escrita passou a justificar handlers.
- Mantem controller fino e sem acesso direto a Infrastructure.
- Preserva consistencia entre solicitacao de estorno e evento por Outbox transacional.
- Evita processamento financeiro sincrono no endpoint HTTP.
- Mantem resposta idempotente estavel para repeticao da mesma operacao.
- Aplica Mediator tambem na leitura de status, mantendo o endpoint HTTP fino e coerente com a politica de queries da ADR-0040.

### Trade-offs / custos
- Aumenta a estrutura do `LedgerService.Application` com command, handler, validator e behavior.
- Introduz mais uma tabela e migration no Ledger.
- O command carrega lista de merchants autorizados para permitir BOLA check depois de carregar o recurso original.
- A query de status tambem carrega lista de merchants autorizados pelo mesmo motivo, pois a rota recebe apenas o `estornoId`.
- O evento de estorno passa a existir antes do consumidor final; operadores podem observar mensagens publicadas sem efeito financeiro imediato.

## Alternativas consideradas

1. Reutilizar `CreateLancamentoService` ou criar outro service sem MediatR.
   - Rejeitada porque o Ledger agora tem mais de uma operacao de escrita e a organizacao por comandos reduz acoplamento no crescimento incremental.

2. Processar o estorno financeiro no endpoint HTTP.
   - Rejeitada porque quebraria o padrao assincrono com Outbox e aumentaria latencia/acoplamento da API.

3. Publicar diretamente no Kafka a partir do controller.
   - Rejeitada porque violaria a ADR-0003 e criaria risco de inconsistencia entre banco e evento.

4. Exigir `merchantId` no body do estorno.
   - Rejeitada para evitar redundancia no contrato e divergencia com o lancamento original; o merchant e derivado do recurso persistido.

## Impacto nos testes
- Foram adicionados testes unitarios para validator e handler do comando de estorno.
- Foram adicionados testes unitarios para validator e handler da query de status de estorno, incluindo estados modelados pelo dominio e erro de recurso inexistente.
- Foram adicionados testes de integracao HTTP para `202`, `400`, `403`, `404`, `409`, idempotencia e persistencia/Outbox.
- Foram adicionados testes de integracao HTTP para consulta de status com `200`, `401`, `403`, `404` e rota invalida.
- A cobertura continua validada pelo fluxo oficial `test.ps1`.

## Impacto operacional
- Aplicar a nova migration do `LedgerService` antes de usar o endpoint em banco novo ou existente.
- O compose passa a criar o topico `ledger.lancamento.estorno.solicitado`.
- Enquanto nao houver consumidor de estorno, o efeito operacional esperado e apenas registrar e publicar a intencao de processamento.
