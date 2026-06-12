function tryOpen(path) {
    try {
        // k6 expoe open() como funcao global.
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
    // 2) .env.k6.auto resolvido a partir deste modulo
    // 3) /.env.k6.auto (quando montado no container)
    const candidates = [];
    if (__ENV.ENV_FILE_PATH) candidates.push(__ENV.ENV_FILE_PATH);
    candidates.push(import.meta.resolve('../../../.env.k6.auto'));
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
        LEDGER_POST_PATH: '/api/v1/lancamentos',
        BALANCE_DAILY_PATH: '/api/v1/consolidados/diario',
        BALANCE_PERIOD_PATH: '/api/v1/consolidados/periodo',

        MERCHANT_ID: 'poc-merchant',
    };

    const fileCfg = loadAutoEnvFile();
    const envCfg = { ...__ENV };
    const cfg = merge(merge(defaults, fileCfg), envCfg);
    return cfg;
}

export function requireTokenOrFail(cfg = loadConfig()) {
    const token = (cfg.TOKEN || '').trim();
    const allowAnon = String(cfg.ALLOW_ANON || '').toLowerCase() === 'true';
    if (!token && !allowAnon) {
        throw new Error('TOKEN vazio. Use scripts/run-loadtests.* ou gere token via scripts/get-token.* e informe env TOKEN=... (ALLOW_ANON=true executa anonimamente).');
    }
    return token;
}
