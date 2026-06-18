import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

import { checkTransferCreated, checkTransferStatus } from '../lib/checks.js';
import { loadConfig } from '../lib/config.js';
import { correlationId, idempotencyKey } from '../lib/ids.js';
import {
    createTransfer,
    getTransfer,
    transferGetSuccess,
    transferPayload,
    transferPostSuccess,
} from '../lib/transfer.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

export const transferCompletedSuccess = new Rate('transfer_completed_success');

const finalStatusTimeoutSeconds = Number(__ENV.TRANSFER_FINAL_STATUS_TIMEOUT_SECONDS || 60);
const pollIntervalSeconds = Number(__ENV.TRANSFER_FINAL_STATUS_POLL_INTERVAL_SECONDS || 1);

export const options = {
    scenarios: {
        transfer_fullstack_kafka: {
            executor: 'shared-iterations',
            vus: Number(__ENV.VUS || 1),
            iterations: Number(__ENV.ITERATIONS || 1),
            maxDuration: __ENV.DURATION || '90s',
        },
    },
    thresholds: {
        transfer_post_success: ['rate==1'],
        transfer_get_success: ['rate==1'],
        transfer_completed_success: ['rate==1'],
        'http_req_failed{service:transfer}': ['rate==0'],
        'http_req_duration{name:transfer_create}': localLatencyThresholds('TRANSFER', { p95: 1000, p99: 2000 }),
        'http_req_duration{name:transfer_get_final_status}': localLatencyThresholds('TRANSFER', { p95: 1000, p99: 2000 }),
        checks: ['rate==1'],
        dropped_iterations: ['count==0'],
    },
};

const cfg = loadConfig();

function waitForCompletedStatus(transferenciaId) {
    const deadline = Date.now() + (finalStatusTimeoutSeconds * 1000);
    let lastStatus = '';

    while (Date.now() < deadline) {
        const res = getTransfer(cfg, transferenciaId, { operation: 'transfer_get_final_status' });
        const getOk = checkTransferStatus(res, transferenciaId);
        transferGetSuccess.add(getOk);

        if (getOk) {
            lastStatus = String(res.json('status') || '');
            if (lastStatus === 'Completed') {
                return true;
            }
        }

        sleep(pollIntervalSeconds);
    }

    check({ lastStatus }, {
        'transfer eventually reaches Completed': (state) => state.lastStatus === 'Completed',
    });
    return false;
}

export default function () {
    const key = idempotencyKey();
    const transferCorrelationId = correlationId();
    const payload = transferPayload(cfg, {
        externalReference: `fullstack-kafka-${key}`,
    });

    const created = createTransfer(cfg, { payload, key, correlation: transferCorrelationId });
    const createdOk = checkTransferCreated(created);
    transferPostSuccess.add(createdOk);

    const transferenciaId = created.json('transferenciaId');
    if (!transferenciaId) {
        transferGetSuccess.add(false);
        transferCompletedSuccess.add(false);
        return;
    }

    const completed = waitForCompletedStatus(transferenciaId);
    transferCompletedSuccess.add(completed);
}
