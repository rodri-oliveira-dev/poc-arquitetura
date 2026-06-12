import { loadConfig } from '../lib/config.js';
import { getDailyBalance } from '../lib/balance.js';
import { checkStatus } from '../lib/checks.js';
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
    const res = getDailyBalance(cfg, { date: cfg.DATE });
    checkStatus(res, 'balance daily summary returns 200', 200);
}
