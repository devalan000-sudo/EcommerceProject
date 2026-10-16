export const cartItemKey = (item) => (item.variantId ? `${item.id}:${item.variantId}` : item.id);
export const quantityLimit = (item) =>
  Number.isInteger(item.availableStock) ? Math.min(99, Math.max(0, item.availableStock)) : 99;

export function parseStoredCart(raw) {
  try {
    const items = JSON.parse(raw);
    if (!Array.isArray(items)) return [];
    const ids = new Set();
    return items.filter((item) => {
      if (
        !item ||
        !Number.isInteger(item.id) ||
        item.id < 1 ||
        ids.has(cartItemKey(item)) ||
        (item.variantId != null && (!Number.isInteger(item.variantId) || item.variantId < 1)) ||
        !Number.isInteger(item.quantity) ||
        item.quantity < 1 ||
        item.quantity > 99 ||
        typeof item.name !== 'string' ||
        !Number.isFinite(item.price) ||
        item.price <= 0
      )
        return false;
      ids.add(cartItemKey(item));
      return true;
    });
  } catch {
    return [];
  }
}

export function reconcileCart(items, products) {
  const available = new Map(products.filter((p) => p.isActive).map((p) => [p.id, p]));
  return items.flatMap((item) => {
    const product = available.get(item.id);
    if (!product) return [];
    const variant = product.variants?.find((v) => v.id === item.variantId && v.isActive);
    if ((item.variantId && !variant) || (!item.variantId && (product.hasVariants || product.variants?.length)))
      return [];
    const updated = {
      ...product,
      ...(variant
        ? {
            imageUrl: variant.imageUrl || product.imageUrl,
            variantId: variant.id,
            size: variant.size,
            color: variant.color,
            availableStock: variant.availableStock,
          }
        : {}),
      quantity: item.quantity,
      ...(typeof item.details === 'string' ? { details: item.details.slice(0, 200) } : {}),
    };
    const limit = quantityLimit(updated);
    return limit > 0 ? [{ ...updated, quantity: Math.min(item.quantity, limit) }] : [];
  });
}
