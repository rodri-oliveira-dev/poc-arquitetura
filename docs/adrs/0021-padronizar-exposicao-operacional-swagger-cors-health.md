# ADR-0021: Padronizar exposicao operacional de Swagger, CORS e health endpoints

## Status
Proposto

## Contexto

LedgerService.Api e BalanceService.Api expõem Swagger na raiz sem gating por ambiente no pipeline atual. `Auth.Api` condiciona Swagger a Development ou `Swagger:Enabled`, mas o compose habilita essa opcao. `/health` e `/ready` sao publicos nas APIs de negocio, e o README documenta esse comportamento como adequado para a POC.

Esses endpoints sao uteis para desenvolvimento e operacao, mas precisam de politica explicita antes de ambientes compartilhados ou produtivos.

## Decisao proposta

Definir uma politica por ambiente para superficies operacionais:

- Swagger/OpenAPI habilitado por padrao somente em desenvolvimento local;
- CORS com origens configuraveis por ambiente e sem ampliar metodos desnecessarios;
- `/health` publico para liveness simples;
- `/ready` publico apenas quando o ambiente/orquestrador exigir, ou protegido/segmentado em rede interna;
- respostas de readiness sem detalhes sensiveis.

## Alternativas consideradas

- Manter todos endpoints operacionais publicos por simplicidade.
- Proteger tambem `/health`, exigindo autenticacao.
- Remover Swagger de todos os ambientes exceto execucao local via host.

## Consequencias positivas

- Reduz exposicao de inventario e detalhes operacionais.
- Mantem compatibilidade com probes simples.
- Facilita alinhar compose, Aspire e producao.

## Consequencias negativas / trade-offs

- Pode exigir configuracao adicional no orquestrador.
- Dificulta debugging em ambientes compartilhados se Swagger for bloqueado.
- Requer testes por ambiente/config.

## Riscos

- Quebrar health checks externos ao proteger endpoints sem coordenacao.
- Divergir documentacao Swagger do comportamento real se houver gating mal documentado.
- Expor detalhes de dependencias em readiness publico.

## Proximos passos sugeridos

- Mapear consumidores de `/health`, `/ready` e Swagger.
- Criar testes de pipeline por ambiente.
- Documentar a matriz de exposicao no README.
- Reaproveitar essa politica ao criar `ServiceDefaults` do Aspire.
