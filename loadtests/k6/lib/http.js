import http from 'k6/http';
import { requireTokenOrFail } from './config.js';

export function defaultHeaders(cfg, extra = {}) {
    const token = requireTokenOrFail(cfg);
    const headers = {
        'Content-Type': 'application/json',
        ...extra,
    };

    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }

    return headers;
}

export function requestParams(cfg, { headers = {}, tags = {} } = {}) {
    return {
        headers: defaultHeaders(cfg, headers),
        tags,
    };
}

export function get(url, params = {}) {
    return http.get(url, params);
}

export function postJson(url, payload, params = {}) {
    return http.post(url, JSON.stringify(payload), params);
}
