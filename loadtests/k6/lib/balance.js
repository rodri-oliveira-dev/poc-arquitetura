import { buildUrl, get, operationParams } from './http.js';

const DAILY_SUMMARY_OPERATION = 'balance_daily_summary';

export function getDailyBalance(cfg, { date, merchantId } = {}) {
    const resolvedDate = date || new Date().toISOString().slice(0, 10);
    const resolvedMerchantId = merchantId || cfg.MERCHANT_ID;
    const url = buildUrl(cfg.BASE_URL_BALANCE, cfg.BALANCE_DAILY_PATH, {
        segments: [resolvedDate],
        query: { merchantId: resolvedMerchantId },
    });

    return get(url, operationParams(cfg, {
        service: 'balance',
        operation: DAILY_SUMMARY_OPERATION,
    }));
}
