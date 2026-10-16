import { reconcileCart, cartItemKey, quantityLimit } from './cart.js';

export function cartReducer(items, action) {
  switch (action.type) {
    case 'sync':
      return reconcileCart(items, action.products);
    case 'add': {
      if (
        quantityLimit(action.product) === 0 ||
        ((action.product.hasVariants || action.product.variants?.length) && !action.product.variantId)
      )
        return items;
      const existing = items.find((item) => cartItemKey(item) === cartItemKey(action.product));
      return existing
        ? items.map((item) =>
            cartItemKey(item) === cartItemKey(existing)
              ? {
                  ...action.product,
                  details: item.details,
                  quantity: Math.min(quantityLimit(action.product), item.quantity + 1),
                }
              : item,
          )
        : [...items, { ...action.product, quantity: 1 }];
    }
    case 'quantity':
      return items.map((item) =>
        cartItemKey(item) === action.id
          ? { ...item, quantity: Math.min(quantityLimit(item), Math.max(1, action.quantity)) }
          : item,
      );
    case 'details':
      return items.map((item) =>
        cartItemKey(item) === action.id ? { ...item, details: action.details.slice(0, 200) } : item,
      );
    case 'remove':
      return items.filter((item) => cartItemKey(item) !== action.id);
    default:
      return items;
  }
}
