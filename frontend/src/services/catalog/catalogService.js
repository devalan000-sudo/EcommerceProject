import { getCatalog } from '../../services/api/catalog.js';

export function loadCatalog(signal) {
  return Promise.all([getCatalog('/products', { signal }), getCatalog('/categories', { signal })]);
}

export function loadProduct(productId, signal) {
  return getCatalog(`/products/${productId}`, { signal });
}
