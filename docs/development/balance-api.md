# BalanceService API

Este documento resume os contratos HTTP de leitura do `BalanceService.Api`.

Os endpoints consultam a projecao `daily_balances`, atualizada de forma assincrona pelo `BalanceService.Worker` a partir de eventos `LedgerEntryCreated.v1`. Quando nao ha dados para a data ou periodo, a API retorna `200 OK` com totais zerados e, no periodo, lista vazia.

## Consolidado diario

`GET /api/v1/consolidados/diario/{date}?merchantId={merchantId}`

Parametros:

| Parametro | Origem | Regra |
| --- | --- | --- |
| `date` | rota | Obrigatorio no formato `YYYY-MM-DD`. |
| `merchantId` | query | Obrigatorio e autorizado na claim `merchant_id`. |

Headers relevantes:

- `Authorization: Bearer <token>` com scope `balance.read`;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

Resposta:

```json
{
  "merchantId": "m1",
  "date": "2026-05-25",
  "currency": "BRL",
  "totalCredits": "150.00",
  "totalDebits": "20.00",
  "netBalance": "130.00",
  "asOf": "2026-05-25T10:30:00.0000000+00:00",
  "calculatedAt": "2026-05-25T10:31:00.0000000+00:00"
}
```

## Consolidado por periodo

`GET /api/v1/consolidados/periodo?merchantId={merchantId}&from={YYYY-MM-DD}&to={YYYY-MM-DD}`

Parametros:

| Parametro | Origem | Regra |
| --- | --- | --- |
| `merchantId` | query | Obrigatorio e autorizado na claim `merchant_id`. |
| `from` | query | Obrigatorio no formato `YYYY-MM-DD`. |
| `to` | query | Obrigatorio no formato `YYYY-MM-DD`, maior ou igual a `from`. |
| periodo | query | Maximo inclusivo definido por `ApiLimits:MaxBalancePeriodDays` (31 dias por padrao). |

Headers relevantes:

- `Authorization: Bearer <token>` com scope `balance.read`;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

Resposta:

```json
{
  "merchantId": "m1",
  "from": "2026-05-01",
  "to": "2026-05-02",
  "currency": "BRL",
  "totalCredits": "150.00",
  "totalDebits": "20.00",
  "netBalance": "130.00",
  "items": [
    {
      "date": "2026-05-01",
      "totalCredits": "150.00",
      "totalDebits": "0.00",
      "netBalance": "150.00",
      "asOf": "2026-05-01T10:30:00.0000000+00:00"
    }
  ],
  "calculatedAt": "2026-05-25T10:31:00.0000000+00:00"
}
```

## Respostas esperadas

| Status | Quando ocorre |
| --- | --- |
| `200 OK` | Consulta processada, com dados projetados ou totais zerados. |
| `400 Bad Request` | Data, query string ou periodo invalido. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant informado. |
| `429 Too Many Requests` | Rate limit excedido. |
| `500 Internal Server Error` | Erro inesperado. |

`GET /health` e `GET /ready` sao endpoints operacionais publicos da API e estao documentados em [observabilidade e operacao minima](../observability.md#endpoints-operacionais).
