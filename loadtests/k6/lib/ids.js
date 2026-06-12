let sequence = 0;

export function loadTestId(prefix) {
    sequence += 1;
    return `${prefix}-${__VU}-${__ITER}-${Date.now()}-${sequence}`;
}

export function idempotencyKey() {
    return loadTestId('idempotency');
}

export function correlationId() {
    return loadTestId('correlation');
}
