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

export function operationParams(cfg, { headers = {}, service, operation }) {
    return requestParams(cfg, {
        headers,
        tags: {
            name: operation,
            service,
            operation,
        },
    });
}

export function buildUrl(baseUrl, path, { segments = [], query = {} } = {}) {
    const cleanBaseUrl = String(baseUrl || '');
    let baseUrlEnd = cleanBaseUrl.length;
    while (baseUrlEnd > 0 && cleanBaseUrl[baseUrlEnd - 1] === '/') {
        baseUrlEnd -= 1;
    }

    const normalizedBaseUrl = cleanBaseUrl.slice(0, baseUrlEnd);
    const pathText = String(path || '');
    const cleanPath = pathText.startsWith('/') ? pathText : `/${pathText}`;
    const encodedSegments = segments
        .filter((segment) => segment !== undefined && segment !== null && segment !== '')
        .map((segment) => encodeURIComponent(segment));

    const queryString = Object.entries(query)
        .filter(([, value]) => value !== undefined && value !== null && value !== '')
        .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`)
        .join('&');

    const segmentPath = encodedSegments.length ? `/${encodedSegments.join('/')}` : '';
    const url = `${normalizedBaseUrl}${cleanPath}${segmentPath}`;
    return queryString ? `${url}?${queryString}` : url;
}

export function get(url, params = {}) {
    return http.get(url, params);
}

export function postJson(url, payload, params = {}) {
    return http.post(url, JSON.stringify(payload), params);
}
