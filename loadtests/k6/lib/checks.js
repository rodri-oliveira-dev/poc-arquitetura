import { check } from 'k6';

export function checkStatus(res, name, expectedStatus) {
    return check(res, {
        [name]: (r) => r.status === expectedStatus,
    });
}

export function checkStatusIn(res, name, expectedStatuses) {
    return check(res, {
        [name]: (r) => expectedStatuses.includes(r.status),
    });
}

export function checkLedgerEntryCreated(res) {
    return checkStatusIn(res, 'ledger create entry returns 201 or idempotent 200', [201, 200]);
}

export function checkBalanceDailySummary(res) {
    return checkStatus(res, 'balance daily summary returns 200', 200);
}

export function checkTransferCreated(res) {
    return check(res, {
        'transfer create returns 202': (r) => r.status === 202,
        'transfer create body has transferenciaId': (r) => !!r.json('transferenciaId'),
        'transfer create body has Pending status': (r) => r.json('status') === 'Pending',
        'transfer create body has statusUrl': (r) => !!r.json('statusUrl'),
        'transfer create has Location header': (r) => !!r.headers.Location,
    });
}

export function checkTransferStatus(res, expectedId) {
    return check(res, {
        'transfer get returns 200': (r) => r.status === 200,
        'transfer get returns same id': (r) => r.json('transferenciaId') === expectedId,
    });
}
