import { check, sleep } from 'k6';
import { loadConfig } from '../lib/config.js';
import { defaultHeaders, httpPost } from '../lib/http.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

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
        'http_req_failed{name:ledger_create_entry}': ['rate<=0.05'],
        'http_req_duration{name:ledger_create_entry}': localLatencyThresholds('LEDGER', { p95: 2000, p99: 5000 }),
        checks: ['rate==1'],
        dropped_iterations: ['count==0'],
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
        tags: {
            name: 'ledger_create_entry',
            service: 'ledger',
            operation: 'ledger_create_entry',
        },
    });

    check(res, {
        'ledger create entry returns 201 or idempotent 200': (r) => r.status === 201 || r.status === 200,
    });

    // Em constant-vus, o sleep representa think time e reduz o ritmo por VU.
    sleep(0.2);
}
