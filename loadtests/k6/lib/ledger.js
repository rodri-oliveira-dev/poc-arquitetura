import { correlationId, idempotencyKey } from './ids.js';
import { buildUrl, operationParams, postJson } from './http.js';

const CREATE_ENTRY_OPERATION = 'ledger_create_entry';

export function createLedgerEntry(cfg, overrides = {}) {
    const entryIdempotencyKey = idempotencyKey();
    const entryCorrelationId = correlationId();
    const merchantId = overrides.merchantId || cfg.MERCHANT_ID;

    const payload = {
        type: 'CREDIT',
        merchantId,
        amount: 10,
        description: 'k6 resilience test',
        externalReference: `k6-${entryIdempotencyKey}`,
        ...overrides.payload,
    };

    const url = buildUrl(cfg.BASE_URL_LEDGER, cfg.LEDGER_POST_PATH);

    return postJson(url, payload, operationParams(cfg, {
        headers: {
            'Idempotency-Key': entryIdempotencyKey,
            'X-Correlation-Id': entryCorrelationId,
        },
        service: 'ledger',
        operation: CREATE_ENTRY_OPERATION,
    }));
}
