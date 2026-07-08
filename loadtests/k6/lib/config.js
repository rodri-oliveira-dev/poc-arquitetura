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

function cleanString(value) {
    return String(value || '').trim();
}

function trimTrailingSlashes(value) {
    let end = value.length;
    while (end > 0 && value[end - 1] === '/') {
        end -= 1;
    }

    return value.slice(0, end);
}

function cleanBaseUrl(value) {
    return trimTrailingSlashes(cleanString(value));
}

function cleanPath(value) {
    const path = cleanString(value);
    if (!path) return '/';
    return path.startsWith('/') ? path : `/${path}`;
}

function parseBoolean(value) {
    return cleanString(value).toLowerCase() === 'true';
}

function normalizeConfig(cfg) {
    const ledgerBaseUrl = cleanBaseUrl(cfg.LEDGER_BASE_URL || cfg.BASE_URL_LEDGER);
    const balanceBaseUrl = cleanBaseUrl(cfg.BALANCE_BASE_URL || cfg.BASE_URL_BALANCE);
    const transferBaseUrl = cleanBaseUrl(cfg.TRANSFER_BASE_URL || cfg.BASE_URL_TRANSFER);
    return {
        ...cfg,
        TOKEN: cleanString(cfg.TOKEN),
        ALLOW_ANON: parseBoolean(cfg.ALLOW_ANON),
        BASE_URL_LEDGER: ledgerBaseUrl,
        LEDGER_BASE_URL: ledgerBaseUrl,
        BASE_URL_BALANCE: balanceBaseUrl,
        BALANCE_BASE_URL: balanceBaseUrl,
        BASE_URL_TRANSFER: transferBaseUrl,
        TRANSFER_BASE_URL: transferBaseUrl,
        LEDGER_POST_PATH: cleanPath(cfg.LEDGER_POST_PATH),
        BALANCE_DAILY_PATH: cleanPath(cfg.BALANCE_DAILY_PATH),
        BALANCE_PERIOD_PATH: cleanPath(cfg.BALANCE_PERIOD_PATH),
        TRANSFER_PATH: cleanPath(cfg.TRANSFER_PATH),
        MERCHANT_ID: cleanString(cfg.MERCHANT_ID),
        SOURCE_MERCHANT_ID: cleanString(cfg.SOURCE_MERCHANT_ID),
        DESTINATION_MERCHANT_ID: cleanString(cfg.DESTINATION_MERCHANT_ID),
    };
}

export function loadConfig() {
    const defaults = {
        // Fallback local (host)
        BASE_URL_LEDGER: 'http://localhost:5226',
        BASE_URL_BALANCE: 'http://localhost:5228',
        BASE_URL_TRANSFER: 'http://localhost:5230',
        MESSAGING_PROVIDER: 'Kafka',
        KAFKA_BOOTSTRAP_SERVERS: 'localhost:19092',
        LEDGER_POST_PATH: '/api/v1/lancamentos',
        BALANCE_DAILY_PATH: '/api/v1/consolidados/diario',
        BALANCE_PERIOD_PATH: '/api/v1/consolidados/periodo',
        TRANSFER_PATH: '/api/v1/transferencias',

        MERCHANT_ID: 'poc-merchant',
        SOURCE_MERCHANT_ID: 'm1',
        DESTINATION_MERCHANT_ID: 'm2',
    };

    const fileCfg = loadAutoEnvFile();
    const envCfg = { ...__ENV };
    return normalizeConfig(merge(merge(defaults, fileCfg), envCfg));
}

export function requireTokenOrFail(cfg = loadConfig()) {
    const token = cleanString(cfg.TOKEN);
    const allowAnon = typeof cfg.ALLOW_ANON === 'boolean' ? cfg.ALLOW_ANON : parseBoolean(cfg.ALLOW_ANON);
    if (!token && !allowAnon) {
        throw new Error('TOKEN vazio. Use scripts/run-loadtests.* ou gere token via scripts/get-token.* e informe env TOKEN=... (ALLOW_ANON=true executa anonimamente).');
    }
    return token;
}
