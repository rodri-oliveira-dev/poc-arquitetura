# ADR-0027: Exposicao controlada de Swagger/OpenAPI

## Status
Aceito

## Data
2026-04-25

## Contexto
`LedgerService.Api` e `BalanceService.Api` habilitavam Swagger/OpenAPI sempre no pipeline HTTP. `Auth.Api` ja condicionava a exposicao a `Development` ou `Swagger:Enabled=true`, mas o compose tambem definia `Swagger__Enabled=true`, criando um caminho facil para carregar esse override em ambientes compartilhados por acidente.

Swagger/OpenAPI ajuda no desenvolvimento, testes locais e inspecao de contrato, mas tambem amplia a superficie de reconhecimento da API quando exposto sem necessidade em ambientes compartilhados ou produtivos.

## Decisao
Padronizar a exposicao de Swagger/OpenAPI em `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`:

- habilitar Swagger por padrao somente quando `ASPNETCORE_ENVIRONMENT=Development`;
- exigir `Swagger:Enabled=true` para habilitar Swagger em qualquer outro ambiente;
- manter `Swagger:Enabled=false` nos `appsettings.json` base dos servicos;
- remover o override `Swagger__Enabled=true` do `compose.yaml`, pois o compose local ja executa os servicos como `Development`;
- manter Swagger UI na raiz quando habilitado, preservando as URLs locais ja documentadas.

Ambientes permitidos:

- `Development`: permitido por padrao para execucao local e compose local;
- ambientes compartilhados, homologacao, staging ou producao: permitido apenas por excecao explicita via configuracao operacional controlada.

Excecoes devem ser temporarias, justificadas e preferencialmente protegidas por controles de rede, autenticacao de borda ou janela operacional limitada.

## Consequencias

### Beneficios
- Reduz risco de exposicao acidental de contratos, rotas, exemplos e requisitos de autenticacao em ambientes nao locais.
- Mantem ergonomia local sem exigir configuracao extra para desenvolvimento.
- Alinha os tres servicos na mesma politica operacional.

### Trade-offs / custos
- Debug em ambientes compartilhados passa a exigir configuracao explicita.
- Operacao precisa lembrar de remover ou desabilitar `Swagger:Enabled=true` apos excecoes temporarias.
- A documentacao local precisa deixar claro que as URLs de Swagger so existem quando a politica permite.

### Riscos
- Um ambiente nao local ainda pode expor Swagger se receber `Swagger:Enabled=true` por configuracao externa.
- Um proxy ou gateway pode publicar a rota raiz e `/swagger/*` sem controles adicionais.
- Drift entre configuracao de ambiente e politica documentada pode reintroduzir exposicao indevida.

## Alternativas consideradas

1) **Remover Swagger de todos os ambientes fora do host local**
   - Pros: menor superficie de exposicao.
   - Contras: dificulta validacoes controladas em ambientes compartilhados e reduz flexibilidade operacional.

2) **Manter o comportamento anterior em Ledger e Balance**
   - Pros: nenhuma mudanca de uso para consumidores internos.
   - Contras: mantem exposicao indevida por padrao em qualquer ambiente.

3) **Exigir `Swagger:Enabled=true` inclusive em Development**
   - Pros: politica totalmente explicita.
   - Contras: piora a experiencia local da POC sem ganho proporcional.
