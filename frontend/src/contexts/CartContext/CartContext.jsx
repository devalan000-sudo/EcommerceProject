import { createContext, useContext, useReducer, useEffect, useCallback } from 'react';
import { cartTotal } from '../../utils/money/money.js';
import { cartReducer } from '../../utils/cart/cartReducer.js';
import { cartItemCount, persistCart, readStoredCart } from '../../services/cart/cartService.js';

const CartContext = createContext(null);
export function CartProvider({ children }) {
  const [items, dispatch] = useReducer(cartReducer, undefined, readStoredCart);
  const syncProducts = useCallback((products) => dispatch({ type: 'sync', products }), []);
  useEffect(() => {
    persistCart(items);
  }, [items]);
  return (
    <CartContext.Provider
      value={{
        items,
        syncProducts,
        total: cartTotal(items),
        count: cartItemCount(items),
        add: (product) => dispatch({ type: 'add', product }),
        setQuantity: (id, quantity) => dispatch({ type: 'quantity', id, quantity }),
        remove: (id) => dispatch({ type: 'remove', id }),
        setDetails: (id, details) => dispatch({ type: 'details', id, details }),
      }}
    >
      {children}
    </CartContext.Provider>
  );
}

export function useCart() {
  const cart = useContext(CartContext);
  if (!cart) throw new Error('useCart requiere CartProvider.');
  return cart;
}
