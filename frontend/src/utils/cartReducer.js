import { reconcileCart } from './cart.js';

export function cartReducer(items, action) {
  switch (action.type) {
    case 'sync': return reconcileCart(items, action.products);
    case 'add': {
      const existing = items.find(item => item.id === action.product.id);
      return existing ? items.map(item => item.id === existing.id ? { ...action.product, details: item.details, quantity: Math.min(99, item.quantity + 1) } : item)
        : [...items, { ...action.product, quantity: 1 }];
    }
    case 'quantity': return items.map(item => item.id === action.id ? { ...item, quantity: Math.min(99, Math.max(1, action.quantity)) } : item);
    case 'details': return items.map(item => item.id === action.id ? { ...item, details: action.details.slice(0, 200) } : item);
    case 'remove': return items.filter(item => item.id !== action.id);
    default: return items;
  }
}
