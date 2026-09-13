import { api } from '../api/client.js';
import { isDemoCatalog } from './catalogMode.js';
import { claimView, readCounts } from '../utils/productViews.js';

const namespace = `catalog-views:${isDemoCatalog ? 'demo' : 'live'}:${api.defaults.baseURL}`;
const countsKey = `${namespace}:counts`;

export function withDemoViews(products) {
  let counts = {};
  try { counts = readCounts(window.localStorage, countsKey); } catch { /* Storage opcional. */ }
  return products.map(p => ({ ...p, viewCount: Number.isSafeInteger(counts[p.id]) && counts[p.id] > 0 ? counts[p.id] : 0 }));
}

export async function registerProductView(id) {
  const key = `${namespace}:seen:${id}`;
  try { if (!claimView(window.sessionStorage, key)) return; } catch { /* Storage opcional. */ }
  try {
    if (isDemoCatalog) {
      const counts = readCounts(window.localStorage, countsKey);
      counts[id] = (Number.isSafeInteger(counts[id]) && counts[id] > 0 ? counts[id] : 0) + 1;
      window.localStorage.setItem(countsKey, JSON.stringify(counts));
    } else {
      await api.post(`/products/${id}/view`);
    }
  } catch {
    // Una visita fallida no bloquea la ficha; permite reintentar al volver a abrirla.
    try { window.sessionStorage.removeItem(key); } catch { /* Storage opcional. */ }
  }
}
