export function parseStoredCart(raw) {
  try {
    const items = JSON.parse(raw);
    if (!Array.isArray(items)) return [];
    const ids = new Set();
    return items.filter(item => {
      if (!item || !Number.isInteger(item.id) || item.id < 1 || ids.has(item.id) ||
        !Number.isInteger(item.quantity) || item.quantity < 1 || item.quantity > 99 ||
        typeof item.name !== 'string' || !Number.isFinite(item.price) || item.price <= 0) return false;
      ids.add(item.id);
      return true;
    });
  } catch { return []; }
}

export function reconcileCart(items, products) {
  const available = new Map(products.filter(p => p.isActive).map(p => [p.id, p]));
  return items.filter(item => available.has(item.id)).map(item => ({ ...available.get(item.id), quantity: item.quantity, ...(typeof item.details === 'string' ? { details: item.details.slice(0, 200) } : {}) }));
}
