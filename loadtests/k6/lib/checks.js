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
