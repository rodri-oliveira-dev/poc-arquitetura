let sequence = 0;

function hex(value, length) {
    return Math.abs(value).toString(16).padStart(length, '0').slice(-length);
}

function uuidFromSequence() {
    sequence += 1;

    const now = Date.now();
    const mixed = (now * sequence) + (__VU * 65536) + __ITER;

    return [
        hex(now, 8),
        hex(__VU, 4),
        hex(__ITER, 4),
        hex(sequence, 4),
        hex(mixed, 12),
    ].join('-');
}

export function loadTestId(prefix) {
    sequence += 1;
    return `${prefix}-${__VU}-${__ITER}-${Date.now()}-${sequence}`;
}

export function idempotencyKey() {
    return uuidFromSequence();
}

export function correlationId() {
    return uuidFromSequence();
}
