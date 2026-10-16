export const VIEW_WINDOW_MS = 30 * 60 * 1000;

export function featuredProducts(products, limit = 8) {
  return products
    .filter((p) => p.isActive)
    .sort((a, b) => (b.viewCount || 0) - (a.viewCount || 0) || a.id - b.id)
    .slice(0, limit);
}

export function readCounts(storage, key) {
  try {
    const value = JSON.parse(storage.getItem(key) || '{}');
    return value && typeof value === 'object' && !Array.isArray(value) ? value : {};
  } catch {
    return {};
  }
}

export function claimView(storage, key, now = Date.now()) {
  try {
    const previous = Number(storage.getItem(key));
    if (previous > 0 && now - previous < VIEW_WINDOW_MS) return false;
    storage.setItem(key, String(now));
  } catch {
    /* El catálogo funciona aunque el navegador bloquee el almacenamiento. */
  }
  return true;
}
