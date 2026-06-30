# ADR-0094: Mailpit local para e-mails do IdentityService

## Status
Aceito

## Data
2026-06-26

## Contexto
O desenvolvimento local e os testes precisam validar que o `IdentityService`
gera e envia a mensagem de boas-vindas sem disparar e-mails reais. A solucao
local deve ser descartavel, visivel para o desenvolvedor e nao deve exigir
mudancas na Application.

## Decisao
Usar Mailpit apenas para ambiente Local e cenarios de teste controlados.

No compose local, Mailpit expoe:

- SMTP em `localhost:1025`;
- UI em `http://localhost:8025`.

Dentro da rede Docker, o `IdentityService.Api` usa `mailpit:1025` com
`Email:Provider=Mailpit`. No host, a mesma configuracao pode ser aplicada com
`Mailpit:Host=localhost` e `Mailpit:Port=1025`.

A Application continua usando apenas `IEmailSender`. A selecao entre Mailpit e
Resend acontece na composition root e nos adapters de Infrastructure.

## Consequencias

### Beneficios
- Permite validar e-mails localmente sem envio externo.
- Mantem a Application sem dependencia de SMTP, Mailpit ou ambiente local.
- Facilita testes manuais do cadastro de usuarios pela UI do Mailpit.
- Evita uso de API keys reais no fluxo local.

### Custos e limitacoes
- Mailpit e ferramenta local e nao substitui provider real em ambiente
  compartilhado ou produtivo.
- Mensagens capturadas sao descartaveis e dependem do ciclo de vida do
  container local.
- Testes automatizados devem preferir fake de `IEmailSender` quando o objetivo
  nao for validar o adapter SMTP.

### Impactos operacionais
- `scripts/local/start-stack.*` sobe Mailpit junto com o core funcional.
- A UI de validacao local fica em `http://localhost:8025`.
- Nenhuma alteracao em Application e necessaria para alternar provider.

## Fora do escopo
- Usar Mailpit em producao.
- Persistir historico de e-mails locais como evidencia duravel.
- Tornar Mailpit dependencia obrigatoria de testes unitarios.
