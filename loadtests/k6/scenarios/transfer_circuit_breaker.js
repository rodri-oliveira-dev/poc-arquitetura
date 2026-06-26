import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

import { checkTransferCreated, checkTransferStatus } from '../lib/checks.js';
import { loadConfig } from '../lib/config.js';
import { createTransfer, getTransfer, transferPayload } from '../lib/transfer.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

const phase = String(__ENV.TRANSFER_CIRCUIT_PHASE || 'degraded').toLowerCase();
const targetVus = Number(__ENV.VUS || 5);
const duration = __ENV.DURATION || '30s';
const finalStatusTimeoutSeconds = Number(__ENV.TRANSFER_FINAL_STATUS_TIMEOUT_SECONDS || (phase === 'degraded' ? 8 : 45));
const pollIntervalSeconds = Number(__ENV.TRANSFER_FINAL_STATUS_POLL_INTERVAL_SECONDS || 1);
const expectedSuccessRate = Number(__ENV.TRANSFER_RECOVERY_SUCCESS_RATE || 0.90);
const expectedDegradationRate = Number(__ENV.TRANSFER_DEGRADATION_CLASSIFICATION_RATE || 0.90);

export const transferCircuitPostAccepted = new Rate('transfer_circuit_post_accepted');
export const transferCircuitGetOk = new Rate('transfer_circuit_get_ok');
export const transferCircuitExpectedDegradation = new Rate('transfer_circuit_expected_degradation');
export const transferCircuitRecovered = new Rate('transfer_circuit_recovered');
export const transferCircuitAuthFailures = new Rate('transfer_circuit_auth_failures');

const thresholds = {
    transfer_circuit_post_accepted: ['rate>=0.99'],
    transfer_circuit_get_ok: ['rate>=0.99'],
    transfer_circuit_auth_failures: ['rate==0'],
    'http_req_failed{service:transfer}': ['rate<0.01'],
    'http_req_duration{name:transfer_create}': localLatencyThresholds('TRANSFER', { p95: 1000, p99: 2000 }),
    'http_req_duration{name:transfer_circuit_status}': localLatencyThresholds('TRANSFER', { p95: 1000, p99: 2000 }),
    checks: ['rate>=0.99'],
    dropped_iterations: ['count==0'],
};

if (phase === 'degraded') {
    thresholds.transfer_circuit_expected_degradation = [`rate>=${expectedDegradationRate}`];
} else {
    thresholds.transfer_circuit_recovered = [`rate>=${expectedSuccessRate}`];
}

function scenarioOptions() {
    const iterations = Number(__ENV.ITERATIONS || 0);
    if (Number.isFinite(iterations) && iterations > 0) {
        return {
            executor: 'shared-iterations',
            vus: targetVus,
            iterations,
            maxDuration: duration,
            gracefulStop: '5s',
        };
    }

    return {
        executor: 'constant-vus',
        vus: targetVus,
        duration,
        gracefulStop: '5s',
    };
}

export const options = {
    scenarios: {
        transfer_circuit_breaker: scenarioOptions(),
    },
    thresholds,
};

const cfg = loadConfig();

function isAuthFailure(res) {
    return res && (res.status === 401 || res.status === 403);
}

function readStatus(transferenciaId) {
    const res = getTransfer(cfg, transferenciaId, {
        operation: 'transfer_circuit_status',
        expectedStatuses: [200],
    });

    const ok = checkTransferStatus(res, transferenciaId);
    transferCircuitGetOk.add(ok);
    transferCircuitAuthFailures.add(isAuthFailure(res));

    return ok ? String(res.json('status') || '') : '';
}

function waitForStatus(transferenciaId) {
    const deadline = Date.now() + (finalStatusTimeoutSeconds * 1000);
    let lastStatus = readStatus(transferenciaId);

    while (Date.now() < deadline) {
        if (lastStatus === 'Completed' || lastStatus === 'Failed' || lastStatus === 'Rejected' || lastStatus === 'Compensated') {
            return lastStatus;
        }

        sleep(pollIntervalSeconds);
        lastStatus = readStatus(transferenciaId);
    }

    return lastStatus;
}

export default function () {
    const payload = transferPayload(cfg, {
        externalReference: `circuit-${phase}-${__VU}-${__ITER}-${Date.now()}`,
    });

    const created = createTransfer(cfg, { payload });
    const createdOk = checkTransferCreated(created);
    transferCircuitPostAccepted.add(createdOk);
    transferCircuitAuthFailures.add(isAuthFailure(created));

    const transferenciaId = created.json('transferenciaId');
    if (!createdOk || !transferenciaId) {
        transferCircuitExpectedDegradation.add(false);
        transferCircuitRecovered.add(false);
        return;
    }

    const finalStatus = waitForStatus(transferenciaId);

    if (phase === 'degraded') {
        const degraded = finalStatus !== 'Completed' && finalStatus !== 'Compensated';
        transferCircuitExpectedDegradation.add(degraded);
        check({ finalStatus }, {
            'transfer remains degraded while Ledger is unavailable': (state) => state.finalStatus !== 'Completed' && state.finalStatus !== 'Compensated',
        });
        return;
    }

    const recovered = finalStatus === 'Completed';
    transferCircuitRecovered.add(recovered);
    check({ finalStatus }, {
        'transfer completes after Ledger recovery': (state) => state.finalStatus === 'Completed',
    });
}
