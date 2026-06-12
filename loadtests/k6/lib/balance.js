import { get, requestParams } from './http.js';

export function getDailyBalance(cfg, { date, merchantId } = {}) {
    const resolvedDate = date || new Date().toISOString().slice(0, 10);
    const resolvedMerchantId = merchantId || cfg.MERCHANT_ID;
    const query = `merchantId=${encodeURIComponent(resolvedMerchantId)}`;
    const url = `${cfg.BASE_URL_BALANCE}${cfg.BALANCE_DAILY_PATH}/${resolvedDate}?${query}`;

    return get(url, requestParams(cfg, {
        tags: {
            name: 'balance_daily_summary',
            service: 'balance',
            operation: 'balance_daily_summary',
        },
    }));
}
