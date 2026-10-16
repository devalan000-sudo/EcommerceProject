import test from 'node:test';
import assert from 'node:assert/strict';
import { cartReducer } from './cartReducer.js';
import { parseStoredCart } from './cart.js';
import { cartTotal } from '../money/money.js';
import { generateWhatsAppMessage } from '../whatsapp/whatsapp.js';

test('conserva preferencias al agregar, restaurar y actualizar el catálogo antes de pedir', () => {
  const product = Object.freeze({ id: 1, name: 'Playera Essential', price: 349, isActive: true });
  const added = cartReducer([], { type: 'add', product });
  const described = cartReducer(added, { type: 'details', id: 1, details: 'una roja y otra negra' });
  Object.freeze(described[0]);
  Object.freeze(described);
  const doubled = cartReducer(described, { type: 'add', product });
  const restored = parseStoredCart(JSON.stringify(doubled));
  const updated = cartReducer(restored, { type: 'sync', products: [{ ...product, price: 399 }] });

  assert.equal(added[0].quantity, 1);
  assert.equal(described[0].quantity, 1);
  assert.equal(updated.length, 1);
  assert.equal(updated[0].quantity, 2);
  assert.equal(updated[0].details, 'una roja y otra negra');
  assert.equal(cartTotal(updated), 798);
  assert.match(generateWhatsAppMessage(updated, cartTotal(updated)), /2x Playera Essential: una roja y otra negra/);
  assert.deepEqual(cartReducer(updated, { type: 'remove', id: 1 }), []);
});

test('mantiene los límites del carrito y no modifica los otros productos', () => {
  const items = [
    { id: 1, name: 'Gorra', price: 100, quantity: 99 },
    { id: 2, name: 'Tenis', price: 200, quantity: 2 },
  ];
  assert.equal(cartReducer(items, { type: 'add', product: items[0] })[0].quantity, 99);
  assert.equal(cartReducer(items, { type: 'quantity', id: 1, quantity: 0 })[0].quantity, 1);
  const limited = cartReducer(items, { type: 'quantity', id: 1, quantity: 200 });
  assert.equal(limited[0].quantity, 99);
  assert.strictEqual(limited[1], items[1]);
  assert.equal(cartReducer(items, { type: 'details', id: 1, details: 'x'.repeat(250) })[0].details.length, 200);
  assert.strictEqual(cartReducer(items, { type: 'unknown' }), items);
});
