import { check } from 'k6';
import { loadConfig } from '../lib/config.js';
import { defaultHeaders, httpGet } from '../lib/http.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

export const options = {
    scenarios: {
        balance_daily_50rps: {
            executor: 'constant-arrival-rate',
            rate: Number(__ENV.RATE || 50),
            timeUnit: '1s',
            duration: __ENV.DURATION || '1m',
            preAllocatedVUs: Number(__ENV.PREALLOCATED_VUS || 50),
            maxVUs: Number(__ENV.MAX_VUS || 200),
        },
    },
    thresholds: {
        'http_req_failed{name:balance_daily_summary}': ['rate<=0.05'],
        'http_req_duration{name:balance_daily_summary}': localLatencyThresholds('BALANCE', { p95: 1000, p99: 2500 }),
        checks: ['rate==1'],
        dropped_iterations: ['count==0'],
    },
};

const cfg = loadConfig();

export default function () {
    const date = __ENV.DATE || new Date().toISOString().slice(0, 10);
    const merchantId = __ENV.MERCHANT_ID || cfg.MERCHANT_ID;
    const base = cfg.BASE_URL_BALANCE;
    const pathBase = cfg.BALANCE_DAILY_PATH;
    const url = `${base}${pathBase}/${date}?merchantId=${encodeURIComponent(merchantId)}`;

    const res = httpGet(url, {
        headers: defaultHeaders(),
        tags: {
            name: 'balance_daily_summary',
            service: 'balance',
            operation: 'balance_daily_summary',
        },
    });

    check(res, {
        'balance daily summary returns 200': (r) => r.status === 200,
    });
}
