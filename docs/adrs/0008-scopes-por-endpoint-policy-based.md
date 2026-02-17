# ADR-0008: Autorização por scopes por endpoint (policy-based)

## Status
Aceito

## Data
2026-02-17

## Contexto
Além de autenticar usuários (JWT válido), precisamos controlar permissões por rota:

- `LedgerService.Api` deve exigir permissão de escrita (`ledger.write`) para criar lançamentos.
- `BalanceService.Api` deve exigir permissão de leitura (`balance.read`) para consultar consolidado.

Uma abordagem “tudo ou nada” (apenas `[Authorize]`) não atende a granularidade necessária e dificulta evolução quando surgirem novos endpoints.

## Decisão
Adotar autorização **policy-based** no ASP.NET Core, onde cada endpoint de negócio declara a policy `scope:{nomeDoScope}`.

- A claim usada é `scope` (string com scopes separados por espaço).
- Existe um catálogo local de scopes e helpers para registrar policies.
- Swagger documenta os scopes requeridos por operação (derivado da policy aplicada).

## Consequências

### Benefícios
- Granularidade por endpoint, com baixo acoplamento.
- Facilita evolução incremental (novos scopes/endpoints).
- Documentação no Swagger fica alinhada ao runtime.

### Trade-offs / custos
- É necessário padronizar naming e semântica dos scopes.
- Exige que o emissor do token mantenha consistência na claim `scope`.
- Pode haver necessidade futura de mapear roles/grupos para scopes (especialmente ao migrar para Keycloak).

## Alternativas consideradas

1) **Roles** em vez de scopes
   - Prós: comum em RBAC.
   - Contras: menos expressivo para APIs; pode ficar “grosso” e difícil de compor.

2) **Atributos customizados por permissão**
   - Prós: liberdade total.
   - Contras: reinventa mecanismo; menos alinhado ao framework.
