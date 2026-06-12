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
