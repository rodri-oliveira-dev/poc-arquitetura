import { sleep } from 'k6';
import { loadConfig } from '../lib/config.js';
import { checkLedgerEntryCreated } from '../lib/checks.js';
import { createLedgerEntry } from '../lib/ledger.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

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
    const res = createLedgerEntry(cfg);
    checkLedgerEntryCreated(res);

    // Em constant-vus, o sleep representa think time e reduz o ritmo por VU.
    sleep(0.2);
}
