import { check } from 'k6';

import { checkTransferCreated, checkTransferStatus } from '../lib/checks.js';
import { loadConfig } from '../lib/config.js';
import { correlationId, idempotencyKey } from '../lib/ids.js';
import {
    createTransfer,
    createTransferExpectingStatus,
    createTransferWithoutIdempotencyKey,
    getTransfer,
    transferGetSuccess,
    transferIdempotencyConflictSuccess,
    transferIdempotencyReplaySuccess,
    transferPayload,
    transferPostSuccess,
} from '../lib/transfer.js';
import { localLatencyThresholds } from '../lib/thresholds.js';

export const options = {
    scenarios: {
        transfer_smoke: {
            executor: 'shared-iterations',
            vus: 1,
            iterations: 1,
            maxDuration: __ENV.DURATION || '30s',
        },
    },
    thresholds: {
        transfer_post_success: ['rate==1'],
        transfer_get_success: ['rate==1'],
        transfer_idempotency_replay_success: ['rate==1'],
        transfer_idempotency_conflict_success: ['rate==1'],
        'http_req_failed{service:transfer}': ['rate==0'],
        'http_req_duration{name:transfer_create}': localLatencyThresholds('TRANSFER', { p95: 10000, p99: 15000 }),
        'http_req_duration{name:transfer_get}': localLatencyThresholds('TRANSFER', { p95: 10000, p99: 15000 }),
        checks: ['rate==1'],
        dropped_iterations: ['count==0'],
    },
};

const cfg = loadConfig();

export default function () {
    const key = idempotencyKey();
    const payload = transferPayload(cfg, {
        externalReference: `smoke-transfer-${key}`,
    });

    const created = createTransfer(cfg, { payload, key, correlation: correlationId() });
    const createdOk = checkTransferCreated(created);
    transferPostSuccess.add(createdOk);

    const transferenciaId = created.json('transferenciaId');
    if (transferenciaId) {
        const found = getTransfer(cfg, transferenciaId);
        const foundOk = checkTransferStatus(found, transferenciaId)
            && check(found, {
                'transfer get returns source merchant': (r) => r.json('sourceMerchantId') === cfg.SOURCE_MERCHANT_ID,
                'transfer get returns destination merchant': (r) => r.json('destinationMerchantId') === cfg.DESTINATION_MERCHANT_ID,
            });
        transferGetSuccess.add(foundOk);
    } else {
        transferGetSuccess.add(false);
    }

    const replay = createTransfer(cfg, {
        payload,
        key,
        correlation: correlationId(),
        operation: 'transfer_idempotent_replay',
    });
    const replayOk = check(replay, {
        'transfer idempotent replay returns 202': (r) => r.status === 202,
        'transfer idempotent replay returns same id': (r) => r.json('transferenciaId') === transferenciaId,
    });
    transferIdempotencyReplaySuccess.add(replayOk);

    const conflict = createTransferExpectingStatus(cfg, [409], {
        payload: {
            ...payload,
            amount: payload.amount + 1,
        },
        key,
        correlation: correlationId(),
        operation: 'transfer_idempotency_conflict',
    });
    const conflictOk = check(conflict, {
        'transfer idempotency conflict returns 409': (r) => r.status === 409,
    });
    transferIdempotencyConflictSuccess.add(conflictOk);

    const notFound = getTransfer(cfg, '11111111-1111-4111-8111-111111111111', {
        operation: 'transfer_get_not_found',
        expectedStatuses: [404],
    });
    check(notFound, {
        'transfer get unknown id returns 404': (r) => r.status === 404,
    });

    const unauthorizedPost = createTransferExpectingStatus(cfg, [401], {
        payload: transferPayload(cfg),
        operation: 'transfer_post_unauthorized',
        authorized: false,
    });
    check(unauthorizedPost, {
        'transfer post without authorization returns 401': (r) => r.status === 401,
    });

    const unauthorizedGet = getTransfer(cfg, '00000000-0000-0000-0000-000000000000', {
        operation: 'transfer_get_unauthorized',
        expectedStatuses: [401],
        authorized: false,
    });
    check(unauthorizedGet, {
        'transfer get without authorization returns 401': (r) => r.status === 401,
    });

    const missingIdempotency = createTransferWithoutIdempotencyKey(cfg, transferPayload(cfg));
    check(missingIdempotency, {
        'transfer post without idempotency key returns 400': (r) => r.status === 400,
    });

    const invalidAmount = createTransferExpectingStatus(cfg, [400], {
        payload: transferPayload(cfg, { amount: 0 }),
        operation: 'transfer_post_invalid_amount',
    });
    check(invalidAmount, {
        'transfer post amount zero returns 400': (r) => r.status === 400,
    });

    const sameMerchant = createTransferExpectingStatus(cfg, [400], {
        payload: transferPayload(cfg, { destinationMerchantId: cfg.SOURCE_MERCHANT_ID }),
        operation: 'transfer_post_same_merchant',
    });
    check(sameMerchant, {
        'transfer post same merchant returns 400': (r) => r.status === 400,
    });

    const forbiddenMerchant = createTransferExpectingStatus(cfg, [403], {
        payload: transferPayload(cfg, { sourceMerchantId: 'merchant-not-authorized' }),
        operation: 'transfer_post_forbidden_merchant',
    });
    check(forbiddenMerchant, {
        'transfer post unauthorized merchant returns 403': (r) => r.status === 403,
    });
}
