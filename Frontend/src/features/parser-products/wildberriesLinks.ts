export function getWildberriesProductUrl(value: string | number | null | undefined): string | null {
  const productId = String(value ?? '').trim();
  return /^\d+$/.test(productId)
    ? `https://www.wildberries.ru/catalog/${encodeURIComponent(productId)}/detail.aspx`
    : null;
}
