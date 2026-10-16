import test from 'node:test';
import assert from 'node:assert/strict';
import { parseStoredCart, reconcileCart } from './cart.js';

test('recupera el carrito y descarta almacenamiento corrupto o cantidades inválidas', () => {
  const item = { id: 1, name: 'Café', price: 25, quantity: 2 };
  assert.deepEqual(parseStoredCart(JSON.stringify([item, item, { ...item, id: 2, quantity: -1 }, null])), [item]);
  assert.deepEqual(parseStoredCart('invalid'), []);
  assert.deepEqual(parseStoredCart('{}'), []);
});
test('actualiza precios y elimina productos inactivos o eliminados conservando cantidades', () => {
  const items = [
    { id: 1, price: 25, quantity: 3 },
    { id: 2, quantity: 1 },
    { id: 3, quantity: 1 },
  ];
  const products = [
    { id: 1, name: 'Café', price: 30, isActive: true },
    { id: 2, isActive: false },
  ];
  assert.deepEqual(reconcileCart(items, products), [{ ...products[0], quantity: 3 }]);
});

test('descarta registros persistidos inválidos y conserva los válidos', () => {
  const valid = { id: 1, name: 'Playera', price: 100, quantity: 2 };
  const invalid = [
    { ...valid, id: 0 },
    { ...valid, id: 2, quantity: 100 },
    { ...valid, id: 3, quantity: 1.5 },
    { ...valid, id: 4, price: '100' },
    { ...valid, id: 5, name: null },
    { ...valid, id: 6, price: 0 },
  ];
  assert.deepEqual(parseStoredCart(JSON.stringify([...invalid, valid])), [valid]);
});
