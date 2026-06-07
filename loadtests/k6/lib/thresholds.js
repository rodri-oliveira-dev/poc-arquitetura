function envNumber(name, fallback) {
    const value = Number(__ENV[name]);
    return Number.isFinite(value) && value > 0 ? value : fallback;
}

export function localLatencyThresholds(prefix, defaults) {
    const p95 = envNumber(`${prefix}_HTTP_REQ_DURATION_P95_MS`, envNumber('K6_HTTP_REQ_DURATION_P95_MS', defaults.p95));
    const p99 = envNumber(`${prefix}_HTTP_REQ_DURATION_P99_MS`, envNumber('K6_HTTP_REQ_DURATION_P99_MS', defaults.p99));

    return [`p(95)<${p95}`, `p(99)<${p99}`];
}
