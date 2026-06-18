import http from 'k6/http';
import { Rate } from 'k6/metrics';

import { correlationId, idempotencyKey, loadTestId } from './ids.js';
import { buildUrl, get, operationParams, postJson, requestParams } from './http.js';

const SERVICE = 'transfer';

export const transferPostSuccess = new Rate('transfer_post_success');
export const transferGetSuccess = new Rate('transfer_get_success');
export const transferIdempotencyReplaySuccess = new Rate('transfer_idempotency_replay_success');
export const transferIdempotencyConflictSuccess = new Rate('transfer_idempotency_conflict_success');

export function transferPayload(cfg, overrides = {}) {
    const key = overrides.externalReference || loadTestId('k6-transfer');
    return {
        sourceMerchantId: cfg.SOURCE_MERCHANT_ID,
        destinationMerchantId: cfg.DESTINATION_MERCHANT_ID,
        amount: 100,
        description: 'Transferencia k6 smoke test',
        externalReference: key,
        ...overrides,
    };
}

export function createTransfer(cfg, { payload = null, key = null, correlation = null, operation = 'transfer_create' } = {}) {
    const transferIdempotencyKey = key || idempotencyKey();
    const transferCorrelationId = correlation || correlationId();
    const body = payload || transferPayload(cfg);
    const url = buildUrl(cfg.BASE_URL_TRANSFER, cfg.TRANSFER_PATH);

    return postJson(url, body, operationParams(cfg, {
        headers: {
            'Idempotency-Key': transferIdempotencyKey,
            'X-Correlation-Id': transferCorrelationId,
        },
        service: SERVICE,
        operation,
    }));
}

export function createTransferExpectingStatus(
    cfg,
    expectedStatuses,
    { payload = null, key = null, correlation = null, operation = 'transfer_create_expected_error', authorized = true } = {}) {
    const transferIdempotencyKey = key || idempotencyKey();
    const transferCorrelationId = correlation || correlationId();
    const body = payload || transferPayload(cfg);
    const url = buildUrl(cfg.BASE_URL_TRANSFER, cfg.TRANSFER_PATH);
    const params = authorized
        ? operationParams(cfg, {
            headers: {
                'Idempotency-Key': transferIdempotencyKey,
                'X-Correlation-Id': transferCorrelationId,
            },
            service: SERVICE,
            operation,
        })
        : requestParams(cfg, {
            headers: {
                'Idempotency-Key': transferIdempotencyKey,
                'X-Correlation-Id': transferCorrelationId,
            },
            tags: {
                name: operation,
                service: SERVICE,
                operation,
            },
        });

    params.responseCallback = http.expectedStatuses(...expectedStatuses);
    if (!authorized) {
        delete params.headers.Authorization;
    }

    return postJson(url, body, params);
}

export function createTransferWithoutIdempotencyKey(cfg, payload = null) {
    const url = buildUrl(cfg.BASE_URL_TRANSFER, cfg.TRANSFER_PATH);
    const params = operationParams(cfg, {
        headers: {
            'X-Correlation-Id': correlationId(),
        },
        service: SERVICE,
        operation: 'transfer_post_missing_idempotency_key',
    });
    params.responseCallback = http.expectedStatuses(400);

    return postJson(url, payload || transferPayload(cfg), params);
}

export function getTransfer(cfg, transferenciaId, { operation = 'transfer_get', expectedStatuses = [200], authorized = true } = {}) {
    const url = buildUrl(cfg.BASE_URL_TRANSFER, cfg.TRANSFER_PATH, { segments: [transferenciaId] });
    const params = authorized
        ? operationParams(cfg, { service: SERVICE, operation })
        : requestParams(cfg, {
            tags: {
                name: operation,
                service: SERVICE,
                operation,
            },
        });

    params.responseCallback = http.expectedStatuses(...expectedStatuses);
    if (!authorized) {
        delete params.headers.Authorization;
    }

    return get(url, params);
}
