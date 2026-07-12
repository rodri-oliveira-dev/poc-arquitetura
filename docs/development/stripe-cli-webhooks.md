# Validacao local de webhooks Stripe com Stripe CLI

Este guia cobre o Prompt 6.5 do fluxo PaymentService + Stripe. O objetivo e
validar localmente a entrada de webhooks reais da Stripe via Stripe CLI antes
de iniciar refund no sandbox.

Fora de escopo: refund, estorno, Stripe Refund, webhook de refund, chargeback,
dispute, reconciliacao e qualquer mudanca na state machine.

## Decisao tecnica

Classificacao desta etapa:

- documentacao e troubleshooting: necessarios para o requisito explicito;
- scripts de listener: uteis, mas opcionais;
- dependencia da Stripe CLI no build/testes: desnecessaria e potencialmente
  prejudicial;
- enfraquecer assinatura do webhook: potencialmente prejudicial.

A suite automatizada deve continuar usando payload assinado localmente, fake
provider, WireMock quando aplicavel e Testcontainers. A Stripe CLI e ferramenta
manual de desenvolvimento, fora do gate.

## Endpoint local canonico

Rota canonica:

```http
POST /api/v1/webhooks/stripe
```

Base local direta do `PaymentService.Api`:

```text
http://localhost:5234
```

Essa porta vem de `src/payment/PaymentService.Api/Properties/launchSettings.json`
e do servidor OpenAPI versionado em `docs/openapi/payment.v1.json`.

No estado atual do repositorio, `compose.yaml` nao publica
`PaymentService.Api` e o overlay Nginx local nao possui host
`payment.localhost`. Portanto, use o modo direto `dotnet run` para este smoke.

## Instalar e verificar Stripe CLI

Use o metodo oficial da Stripe para o seu sistema operacional:
<https://docs.stripe.com/stripe-cli/install>.

No Windows, a documentacao oficial lista, entre outras opcoes:

```powershell
winget install Stripe.StripeCLI
```

ou:

```powershell
scoop bucket add stripe https://github.com/stripe/scoop-stripe-cli.git
scoop install stripe
```

Tambem ha instalacao por ZIP baixado do release oficial. Em macOS, Homebrew e
arquivo tar.gz sao opcoes documentadas. Em Linux, a Stripe documenta pacotes
`apt`/`yum` e arquivo tar.gz.

A documentacao oficial tambem lista npm como opcao:

```bash
npm i -g @stripe/cli
```

Neste repositorio, npm nao e instrucao principal e nao deve ser adicionado ao
`package.json`. Use apenas se fizer sentido na sua maquina e depois valide:

```bash
stripe version
```

Se seu binario responder somente a outra forma, valide tambem:

```bash
stripe --version
```

## Login e sandbox

Autentique a CLI somente em ambiente de teste/sandbox:

```bash
stripe login
```

Regras:

- exige conta Stripe ou sandbox de desenvolvimento;
- nao use chaves `live`;
- nao salve tokens da Stripe CLI no repositorio;
- nao cole `sk_test_...`, `rk_test_...` ou `whsec_...` em arquivo versionado;
- a validacao real deve acontecer em ambiente de teste.

## Configuracao de secrets

`sk_test_...` e API key da Stripe para chamadas da API pelo provider Stripe.
`whsec_...` e webhook signing secret para validar eventos recebidos.

Eles nao sao intercambiaveis.

### SecretKey Stripe

Para criar PaymentIntent e Refund reais no sandbox:

```powershell
dotnet user-secrets set "PaymentGateway:Provider" "Stripe" --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
dotnet user-secrets set "PaymentGateway:Stripe:SecretKey" "sk_test_xxx" --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
dotnet user-secrets set "PaymentGateway:Stripe:SecretKey" "sk_test_xxx" --project ./src/payment/PaymentService.Worker/PaymentService.Worker.csproj
```

Ou na sessao:

```powershell
$env:PaymentGateway__Provider = "Stripe"
$env:PaymentGateway__Stripe__SecretKey = "sk_test_xxx"
```

```bash
export PaymentGateway__Provider=Stripe
export PaymentGateway__Stripe__SecretKey=sk_test_xxx
```

`PaymentGateway:Stripe:SecretKey` e usado para chamadas da API Stripe. O nome
legado `PaymentGateway:Stripe:ApiKey` ainda e aceito como alias local, mas novas
configuracoes devem usar `SecretKey`. O Worker nao chama Stripe no desenho
atual do refund; o secret no Worker e opcional hoje e fica registrado para
cenarios futuros/alternativos de reconciliacao.

### Webhook signing secret

Execute `stripe listen` e copie o valor temporario `whsec_...` impresso pela
CLI:

```powershell
dotnet user-secrets set "PaymentGateway:Stripe:WebhookSigningSecret" "whsec_xxx" --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
```

Ou na sessao:

```powershell
$env:PaymentGateway__Stripe__WebhookSigningSecret = "whsec_xxx"
```

```bash
export PaymentGateway__Stripe__WebhookSigningSecret=whsec_xxx
```

Se usar `.env.local` para processos locais, use apenas placeholder ou valor
local ignorado pelo Git:

```env
PaymentGateway__Provider=Stripe
PaymentGateway__Stripe__SecretKey=sk_test_xxx
PaymentGateway__Stripe__WebhookSigningSecret=whsec_xxx
```

## Iniciar PaymentService.Api

Suba as dependencias necessarias, aplique migrations conforme o fluxo local e
rode a API no host:

```powershell
dotnet run --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
```

A API deve ficar em:

```text
http://localhost:5234
```

O endpoint de webhook nao exige JWT. Ele exige `Stripe-Signature`, raw body
inalterado e `PaymentGateway:Stripe:WebhookSigningSecret`.

## Ouvir eventos com Stripe CLI

Comando canonico:

```bash
stripe listen --forward-to http://localhost:5234/api/v1/webhooks/stripe
```

Para reduzir ruido durante o smoke do MVP:

```bash
stripe listen --events payment_intent.processing,payment_intent.succeeded,payment_intent.payment_failed,payment_intent.canceled,refund.created,refund.updated,refund.failed --forward-to http://localhost:5234/api/v1/webhooks/stripe
```

A CLI imprime uma linha equivalente a:

```text
Ready! Your webhook signing secret is 'whsec_...'
```

Copie esse valor para `PaymentGateway__Stripe__WebhookSigningSecret` ou user
secrets. Nao versione o valor.

Scripts opcionais:

```powershell
./scripts/validation/stripe-listen-payment-webhook.ps1
```

```bash
./scripts/validation/stripe-listen-payment-webhook.sh
```

Eles apenas validam se `stripe` existe, montam a URL canonica e executam
`stripe listen` em primeiro plano. Eles nao instalam a CLI, nao salvam secret e
nao rodam em background.

## Disparar eventos sinteticos

Em outro terminal:

```bash
stripe trigger payment_intent.succeeded
```

Eventos do MVP suportados pela referencia atual do `stripe trigger`:

```bash
stripe trigger payment_intent.succeeded
stripe trigger payment_intent.payment_failed
stripe trigger payment_intent.canceled
```

A referencia da Stripe CLI tambem lista `payment_intent.created`,
`payment_intent.amount_capturable_updated`, `payment_intent.partially_funded` e
`payment_intent.requires_action`, mas estes nao fazem parte do processamento MVP
do Worker. O evento `payment_intent.processing` e suportado pelo modelo do
PaymentService, mas nao aparece na lista atual de triggers da CLI; para validar
esse estado, use fluxo correlacionado real no sandbox ou payload assinado
controlado nos testes automatizados.

## Evento sintetico versus correlacionado

### Evento sintetico

`stripe trigger payment_intent.succeeded` valida:

- entrega HTTP;
- `Stripe-Signature`;
- raw body;
- persistencia na Inbox;
- deduplicacao;
- classificacao do evento;
- comportamento quando nao ha Payment local correspondente.

Ele nao valida necessariamente:

- associacao com Payment criado pelo `PaymentService`;
- transicao real do Payment;
- chamada Payment -> Ledger;
- atualizacao posterior do Balance.

Nao trate esse smoke como E2E completo.

### Evento correlacionado

Para validar o fluxo completo:

1. configure `PaymentGateway__Provider=Stripe`;
2. configure `PaymentGateway__Stripe__SecretKey=sk_test_...`;
3. configure `PaymentGateway__Stripe__WebhookSigningSecret=whsec_...`;
4. crie Payment via `POST /api/v1/payments`;
5. obtenha `providerPaymentId`;
6. confirme ou simule o PaymentIntent correspondente na Stripe Sandbox;
7. receba o webhook pelo `stripe listen`;
8. verifique o Payment local;
9. verifique a integracao com Ledger;
10. verifique Balance apenas se a stack completa estiver ativa.

Como o comando exato para confirmar um PaymentIntent depende do metodo de
pagamento, do estado do PaymentIntent e do fluxo escolhido, valide-o na
documentacao oficial da Stripe antes de registrar um runbook automatizado.

## Smoke A - intake do webhook

Objetivo:

```text
Stripe CLI -> PaymentService webhook -> Inbox
```

Passos:

1. configure `PaymentGateway__Stripe__WebhookSigningSecret` com o `whsec_...`
   do `stripe listen`;
2. rode `PaymentService.Api` em `http://localhost:5234`;
3. execute `stripe listen --forward-to http://localhost:5234/api/v1/webhooks/stripe`;
4. execute `stripe trigger payment_intent.succeeded`;
5. confirme no Stripe CLI resposta `200`;
6. verifique logs da API sem payload integral, secrets ou `clientSecret`;
7. consulte `payment.inbox_messages` no PostgreSQL;
8. confirme que reentrega do mesmo `event.id` nao cria segunda linha.

Consulta auxiliar:

```sql
SELECT provider_event_id, event_type, status, received_at_utc, payload_sha256
FROM payment.inbox_messages
ORDER BY received_at_utc DESC
LIMIT 10;
```

## Smoke B - fluxo correlacionado

Objetivo:

```text
PaymentService -> Stripe PaymentIntent -> webhook correlacionado -> Payment state -> Ledger
```

Passos:

1. suba PostgreSQL, Keycloak e servicos necessarios;
2. configure `PaymentGateway__Provider=Stripe`;
3. configure `PaymentGateway__Stripe__SecretKey=sk_test_...`;
4. configure `PaymentGateway__Stripe__WebhookSigningSecret=whsec_...`;
5. rode `PaymentService.Api`;
6. rode `PaymentService.Worker`;
7. crie Payment via `POST /api/v1/payments`;
8. confirme o PaymentIntent no sandbox com comando oficial adequado ao fluxo;
9. receba o webhook via `stripe listen`;
10. verifique Payment e Inbox;
11. verifique Ledger;
12. verifique Balance, se a stack de Ledger/Balance estiver ativa.

## Smoke C - refund correlacionado

Objetivo:

```text
Payment Completed -> POST refund -> Stripe Refund -> webhook refund -> Inbox -> Worker -> Ledger estorno
```

Passos:

1. conclua o Smoke B ate o Payment ficar `Completed`;
2. mantenha `stripe listen` apontando para `http://localhost:5234/api/v1/webhooks/stripe`;
3. chame `POST /api/v1/payments/{paymentId}/refunds` com JWT que tenha scope
   `payment.refund`, merchant autorizado, `Idempotency-Key` UUID e payload de
   refund total;
4. aguarde eventos reais `refund.created`, `refund.updated` ou `refund.failed`;
5. verifique `payment.inbox_messages`;
6. rode `PaymentService.Worker`;
7. confirme que o refund chegou a `Completed` ou ficou pendente de estorno com
   retry persistido;
8. verifique no Ledger a solicitacao em
   `POST /api/v1/lancamentos/{ledgerEntryId}/estornos`;
9. verifique Balance apenas pela propagacao eventual do Ledger, se a stack
   completa estiver ativa.

Nao assuma que `stripe trigger refund.created` esta disponivel em todas as
versoes da CLI. Valide com `stripe trigger --help`; se o trigger sintetico nao
existir, use o refund real criado pelo PaymentService no sandbox.

## Pre-requisito para Prompt 7

Antes de iniciar refund:

- Stripe CLI instalado e validado com `stripe version`;
- `stripe listen` encaminhando para `http://localhost:5234/api/v1/webhooks/stripe`;
- `whsec_...` correto configurado em `PaymentGateway:Stripe:WebhookSigningSecret`;
- `payment_intent.succeeded` testado localmente;
- Inbox recebendo evento;
- Worker processando eventos suportados;
- logs sem secrets, raw body integral ou `clientSecret`.

## Troubleshooting

### `stripe: command not found`

Instale a Stripe CLI pelo metodo oficial do seu sistema operacional e valide:

```bash
stripe version
```

### `Webhook Stripe nao configurado`

O `PaymentService.Api` retornou `503` porque
`PaymentGateway:Stripe:WebhookSigningSecret` esta vazio. Rode `stripe listen`,
copie o `whsec_...` temporario e configure user secrets ou variavel de ambiente
local.

### `No signatures found matching the expected signature for payload`

Causas provaveis:

- `whsec_...` errado;
- endpoint usado no `stripe listen` nao e `http://localhost:5234/api/v1/webhooks/stripe`;
- body foi alterado antes da validacao;
- header `Stripe-Signature` ausente;
- proxy alterando payload;
- variavel de ambiente nao carregada pelo processo da API.

### Evento chega, mas Payment nao muda

Verifique:

- se o evento foi sintetico e nao possui `metadata.payment_id` local;
- se nao existe Payment local correlacionado;
- se `PaymentService.Worker` esta em execucao;
- se a mensagem esta em `Pending`, `RetryScheduled` ou `DeadLetter`;
- se `last_error` indica Payment ausente, payload incompativel ou retry.

### Evento chega, mas Ledger nao recebe

Verifique:

- se o evento corresponde a Payment real e nao apenas a fixture sintetica;
- se o Payment chegou a `Succeeded`;
- se o Worker de Ledger do PaymentService esta ativo;
- se a integracao esta pendente/retry em `payment.payments`;
- se os logs e metricas indicam falha de token, Ledger indisponivel ou retry.

### `stripe trigger` nao suporta determinado evento

Use um evento listado pela referencia atual da Stripe CLI ou execute o fluxo
correlacionado real no sandbox. Nao invente fixture local como se fosse evento
oficial da CLI.
