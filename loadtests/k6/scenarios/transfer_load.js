import { sleep } from 'k6';

import { checkTransferCreated, checkTransferStatus } from '../lib/checks.js';
import { loadConfig } from '../lib/config.js';
import {
    createTransfer,
    getTransfer,
    transferGetSuccess,
    transferPayload,
    transferPostSuccess,
} from '../lib/transfer.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

const targetVus = Number(__ENV.VUS || 10);

export const options = {
    scenarios: {
        transfer_load: {
            executor: 'ramping-vus',
            stages: [
                { duration: __ENV.RAMP_UP_DURATION || '1m', target: targetVus },
                { duration: __ENV.STEADY_DURATION || '3m', target: targetVus },
                { duration: __ENV.RAMP_DOWN_DURATION || '1m', target: 0 },
            ],
            gracefulRampDown: '10s',
        },
    },
    thresholds: {
        transfer_post_success: ['rate>=0.99'],
        transfer_get_success: ['rate>=0.99'],
        'http_req_failed{service:transfer}': ['rate<0.02'],
        'http_req_duration{name:transfer_create}': localLatencyThresholds('TRANSFER', { p95: 1000, p99: 2000 }),
        'http_req_duration{name:transfer_get}': localLatencyThresholds('TRANSFER', { p95: 1000, p99: 2000 }),
        checks: ['rate>=0.99'],
        dropped_iterations: ['count==0'],
    },
};

const cfg = loadConfig();

export default function () {
    const createRes = createTransfer(cfg, { payload: transferPayload(cfg) });
    const createOk = checkTransferCreated(createRes);
    transferPostSuccess.add(createOk);

    const transferenciaId = createRes.json('transferenciaId');
    if (transferenciaId) {
        const getRes = getTransfer(cfg, transferenciaId);
        const getOk = checkTransferStatus(getRes, transferenciaId);
        transferGetSuccess.add(getOk);
    } else {
        transferGetSuccess.add(false);
    }

    // Em ramping-vus, o sleep representa think time simples entre operacoes.
    sleep(Number(__ENV.SLEEP_SECONDS || 0.2));
}
