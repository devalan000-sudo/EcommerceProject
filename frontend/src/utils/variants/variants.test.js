import test from 'node:test';
import assert from 'node:assert/strict';
import { cartReducer } from '../cart/cartReducer.js';
import { cartItemKey, parseStoredCart, reconcileCart } from '../cart/cart.js';
import { generateWhatsAppMessage } from '../whatsapp/whatsapp.js';

const product = {
  id: 1,
  name: 'Playera',
  price: 100,
  isActive: true,
  hasVariants: true,
  variants: [
    { id: 10, size: 'M', color: 'ROJO', availableStock: 2, isActive: true },
    { id: 11, size: 'L', color: 'NEGRO', availableStock: 1, isActive: true },
  ],
};
const select = (v) => ({ ...product, variantId: v.id, size: v.size, color: v.color, availableStock: v.availableStock });

test('separa colores del mismo producto y limita cantidades por variante', () => {
  let items = cartReducer([], { type: 'add', product: select(product.variants[0]) });
  items = cartReducer(items, { type: 'add', product: select(product.variants[1]) });
  items = cartReducer(items, { type: 'add', product: select(product.variants[1]) });
  assert.equal(items.length, 2);
  assert.equal(items[1].quantity, 1);
  const restored = parseStoredCart(JSON.stringify(items));
  assert.equal(restored.length, 2);
  const message = generateWhatsAppMessage(restored, 200);
  assert.ok(message.includes('[M / ROJO]'));
  assert.ok(message.includes('[L / NEGRO]'));
  assert.equal(cartReducer(items, { type: 'remove', id: cartItemKey(items[0]) })[0].variantId, 11);
});

test('reconcilia stock nuevo y elimina variantes agotadas o inactivas', () => {
  const items = product.variants.map((v) => ({ ...select(v), quantity: 2 }));
  const updated = {
    ...product,
    variants: [
      { ...product.variants[0], availableStock: 1 },
      { ...product.variants[1], isActive: false },
    ],
  };
  assert.equal(reconcileCart(items, [updated]).length, 1);
  assert.equal(reconcileCart(items, [updated])[0].quantity, 1);
  assert.deepEqual(reconcileCart(items, [{ ...product, variants: [] }]), []);
});

test('no agrega variantes agotadas ni productos configurados sin selección', () => {
  assert.deepEqual(cartReducer([], { type: 'add', product }), []);
  assert.deepEqual(
    cartReducer([], { type: 'add', product: { ...select(product.variants[0]), availableStock: 0 } }),
    [],
  );
  assert.deepEqual(reconcileCart([{ id: 1, quantity: 1 }], [product]), []);
});
