function tryOpen(path) {
    try {
        // k6 expõe open() como função global
        return open(path);
    } catch (_) {
        return null;
    }
}

function parseEnvFileContent(content) {
    const obj = {};
    if (!content) return obj;

    const lines = content.split(/\r?\n/);
    for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;
        const idx = trimmed.indexOf('=');
        if (idx <= 0) continue;
        const key = trimmed.slice(0, idx).trim();
        const value = trimmed.slice(idx + 1).trim();
        if (!key) continue;
        obj[key] = value;
    }
    return obj;
}

function loadAutoEnvFile() {
    // Ordem de tentativa:
    // 1) __ENV.ENV_FILE_PATH (se informado)
    // 2) .env.k6.auto (working_dir)
    // 3) /.env.k6.auto (quando montado no container)
    const candidates = [];
    if (__ENV.ENV_FILE_PATH) candidates.push(__ENV.ENV_FILE_PATH);
    candidates.push('.env.k6.auto');
    candidates.push('/.env.k6.auto');

    for (const path of candidates) {
        const content = tryOpen(path);
        if (content) return parseEnvFileContent(content);
    }

    return {};
}

function merge(base, overlay) {
    return { ...base, ...overlay };
}

export function loadConfig() {
    const defaults = {
        // Fallback local (host)
        BASE_URL_LEDGER: 'http://localhost:5226',
        BASE_URL_BALANCE: 'http://localhost:5228',
        AUTH_BASE_URL: 'http://localhost:5030',
        TOKEN_URL: '/auth/login',
        LEDGER_POST_PATH: '/api/v1/lancamentos',
        BALANCE_DAILY_PATH: '/v1/consolidados/diario',
        BALANCE_PERIOD_PATH: '/v1/consolidados/periodo',

        MERCHANT_ID: 'poc-merchant',
    };

    const fileCfg = loadAutoEnvFile();
    const envCfg = { ...__ENV };
    const cfg = merge(merge(defaults, fileCfg), envCfg);
    return cfg;
}

export function requireTokenOrFail() {
    const token = (__ENV.TOKEN || '').trim();
    const allowAnon = (__ENV.ALLOW_ANON || '').toLowerCase() === 'true';
    if (!token && !allowAnon) {
        throw new Error('TOKEN vazio. Gere token via scripts/get-token.* ou informe via env TOKEN=... (ou use ALLOW_ANON=true para executar anonimamente).');
    }
    return token;
}
