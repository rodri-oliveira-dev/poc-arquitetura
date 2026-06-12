import { uuidv4 } from './ids.js';
import { postJson, requestParams } from './http.js';

export function createLedgerEntry(cfg, overrides = {}) {
    const idempotencyKey = uuidv4();
    const correlationId = uuidv4();
    const merchantId = overrides.merchantId || cfg.MERCHANT_ID;

    const payload = {
        type: 'CREDIT',
        merchantId,
        amount: 10.0,
        description: 'k6 resilience test',
        externalReference: `k6-${idempotencyKey}`,
        ...overrides.payload,
    };

    const url = `${cfg.BASE_URL_LEDGER}${cfg.LEDGER_POST_PATH}`;

    return postJson(url, payload, requestParams(cfg, {
        headers: {
            'Idempotency-Key': idempotencyKey,
            'X-Correlation-Id': correlationId,
        },
        tags: {
            name: 'ledger_create_entry',
            service: 'ledger',
            operation: 'ledger_create_entry',
        },
    }));
}
