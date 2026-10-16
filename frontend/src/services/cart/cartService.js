import { isDemoCatalog } from '../../data/catalogMode.js';
import { parseStoredCart } from '../../utils/cart/cart.js';
import { generateWhatsAppLink, generateWhatsAppMessage } from '../../utils/whatsapp/whatsapp.js';

export const cartStorageKey = `catalog-cart:${isDemoCatalog ? 'demo' : 'live'}:${import.meta.env.VITE_API_URL || 'local'}`;

export function readStoredCart() {
  try {
    return parseStoredCart(localStorage.getItem(cartStorageKey));
  } catch {
    return [];
  }
}

export function persistCart(items) {
  try {
    localStorage.setItem(cartStorageKey, JSON.stringify(items));
  } catch {
    // El carrito continúa disponible en memoria si storage no está disponible.
  }
}

export function cartItemCount(items) {
  return items.reduce((total, item) => total + item.quantity, 0);
}

export function cartWhatsAppMessage(items, total, demo) {
  return items.length ? generateWhatsAppMessage(items, total, demo) : '';
}

export function cartWhatsAppLink(items, total, phone, demo) {
  return generateWhatsAppLink(items, total, phone, demo);
}
