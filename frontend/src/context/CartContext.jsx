import { createContext, useContext, useReducer, useEffect, useCallback } from 'react';
import { isDemoCatalog } from '../data/catalogMode.js';
import { cartTotal } from '../utils/money.js';
import { parseStoredCart } from '../utils/cart.js';
import { cartReducer } from '../utils/cartReducer.js';

const CartContext = createContext(null);
const storageKey = `catalog-cart:${isDemoCatalog ? "demo" : "live"}:${import.meta.env.VITE_API_URL || 'local'}`;
function readCart() {
  try { return parseStoredCart(localStorage.getItem(storageKey)); } catch { return []; }
}
export function CartProvider({ children }) {
  const [items, dispatch] = useReducer(cartReducer, undefined, readCart);
  const syncProducts = useCallback(products => dispatch({ type: 'sync', products }), []);
  useEffect(() => {
    try { localStorage.setItem(storageKey, JSON.stringify(items)); } catch { /* El carrito sigue funcionando en memoria. */ }
  }, [items]);
  return <CartContext.Provider value={{ items, syncProducts, total: cartTotal(items), count: items.reduce((n, item) => n + item.quantity, 0),
    add: product => dispatch({ type: 'add', product }),
    setQuantity: (id, quantity) => dispatch({ type: 'quantity', id, quantity }),
    remove: id => dispatch({ type: 'remove', id }),
    setDetails: (id, details) => dispatch({ type: 'details', id, details }),
  }}>{children}</CartContext.Provider>;
}

export function useCart() {
  const cart = useContext(CartContext);
  if (!cart) throw new Error('useCart requiere CartProvider.');
  return cart;
}

