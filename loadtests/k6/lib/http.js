import http from 'k6/http';
import { requireTokenOrFail } from './config.js';

export function defaultHeaders(extra = {}) {
    const token = requireTokenOrFail();
    const headers = {
        'Content-Type': 'application/json',
        ...extra,
    };

    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }

    return headers;
}

export function httpGet(url, params = {}) {
    return http.get(url, params);
}

export function httpPost(url, body, params = {}) {
    return http.post(url, body, params);
}
