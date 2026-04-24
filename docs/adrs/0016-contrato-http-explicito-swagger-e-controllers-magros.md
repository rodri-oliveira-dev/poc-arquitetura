# ADR-0016: Contrato HTTP explícito com Swagger e controllers magros

## Status
Aceito

## Data
2026-04-23

## Contexto
Os endpoints de `LedgerService.Api` e `BalanceService.Api` evoluíram para explicitar melhor o contrato HTTP (status codes, parâmetros e regras de uso de headers) e reduzir lógica de transformação dentro dos controllers.

Na abordagem anterior, havia mais lógica de parse/mapeamento no controller e documentação mais dispersa em comentários XML.

Isso aumentava:

- duplicação de responsabilidade de borda HTTP;
- chance de drift entre comportamento real e documentação de API;
- dificuldade de manter controllers curtos e focados em orquestração.

## Decisão
Padronizar dois princípios para endpoints HTTP desta PoC:

1) **Contrato explícito no Swagger via annotations**
   - Usar `SwaggerOperation`, `SwaggerResponse` e `SwaggerParameter` para declarar, no próprio endpoint, o contrato esperado.
   - Documentar headers relevantes (`Idempotency-Key`, `X-Correlation-Id`) e respostas de erro de forma consistente.

2) **Controllers magros (orquestração) com bind/map em componentes dedicados**
   - Controller deve apenas receber entrada HTTP, delegar para `Application` e retornar resposta.
   - Parse/mapeamento de request e response deve ficar em classes dedicadas (ex.: `*Bind`, `*Mapper`).
   - Validação de transporte HTTP (ex.: header obrigatório/formato UUID) deve ocorrer na borda, sem mover regra de negócio para infraestrutura.

Implementação aplicada em:

- `src/LedgerService.Api/Controllers/LancamentosController.cs`
- `src/LedgerService.Api/Controllers/Binds/CreateLancamentoBind.cs`
- `src/BalanceService.Api/Controllers/ConsolidadosController.cs`
- `src/BalanceService.Api/Mappers/BalanceQueryMapper.cs`
- `src/BalanceService.Api/Mappers/BalanceResponseMapper.cs`

## Consequências

### Benefícios
- Contrato HTTP mais claro para consumidor e para manutenção interna.
- Menor acoplamento do controller com detalhes de parse/formatação.
- Melhor alinhamento com Clean Architecture (Api como borda e orquestração).

### Trade-offs / custos
- Mais classes pequenas para manter (`Bind`/`Mapper`).
- Exige disciplina para manter documentação Swagger sincronizada com comportamento real.

## Alternativas consideradas

1) **Manter comentários XML como fonte principal de contrato**
   - Prós: menos atributos no endpoint.
   - Contras: menor padronização visual e menor clareza operacional em alguns casos.

2) **Manter parse/mapeamento inline no controller**
   - Prós: menos arquivos.
   - Contras: controllers maiores, maior duplicação e menor testabilidade dos pontos de borda.
