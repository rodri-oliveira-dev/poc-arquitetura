import { check, sleep } from 'k6';
import { loadConfig } from '../lib/config.js';
import { defaultHeaders, httpPost } from '../lib/http.js';

function uuidv4() {
    // UUID v4 simples (suficiente para idempotência/correlação em testes)
    // Fonte: implementação comum baseada em Math.random
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

export const options = {
    scenarios: {
        ledger_resilience: {
            executor: 'constant-vus',
            vus: Number(__ENV.VUS || 5),
            duration: __ENV.DURATION || '1m',
        },
    },
    thresholds: {
        http_req_failed: ['rate<=0.05'],
    },
};

const cfg = loadConfig();

export default function () {
    const base = cfg.BASE_URL_LEDGER;
    const path = cfg.LEDGER_POST_PATH;
    const url = `${base}${path}`;
    const merchantId = __ENV.MERCHANT_ID || cfg.MERCHANT_ID;

    const idempotencyKey = uuidv4();
    const correlationId = uuidv4();

    const body = JSON.stringify({
        type: 'CREDIT',
        merchantId,
        amount: 10.0,
        description: 'k6 resilience test',
        externalReference: `k6-${idempotencyKey}`,
    });

    const res = httpPost(url, body, {
        headers: defaultHeaders({
            'Idempotency-Key': idempotencyKey,
            'X-Correlation-Id': correlationId,
        }),
    });

    check(res, {
        'status is 201 or 200': (r) => r.status === 201 || r.status === 200,
    });

    sleep(0.2);
}
