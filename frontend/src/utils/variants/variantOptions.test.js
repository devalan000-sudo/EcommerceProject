import test from 'node:test';
import assert from 'node:assert/strict';
import { availableVariant, imageForColor, sizeLabel } from './variantOptions.js';
import { reconcileCart } from '../cart/cart.js';
import { demoProducts } from '../../data/demoCatalog.js';

test('selección exige combinación disponible y no reutiliza otra talla o color', () => {
  const variants = [
    { id: 1, size: 'S', color: 'AZUL', isActive: true, availableStock: 2 },
    { id: 2, size: 'M', color: 'NEGRO', isActive: true, availableStock: 0 },
  ];
  assert.equal(availableVariant(variants, 'AZUL', 'S').id, 1);
  assert.equal(availableVariant(variants, 'NEGRO', 'S'), undefined);
  assert.equal(availableVariant(variants, 'NEGRO', 'M'), undefined);
  assert.equal(sizeLabel('S'), 'Chica');
  assert.equal(sizeLabel('M'), 'Mediana');
  assert.equal(sizeLabel('L'), 'Grande');
});
test('la foto corresponde al color y se conserva al sincronizar el carrito', () => {
  const product = {
    id: 1,
    isActive: true,
    imageUrl: 'base.jpg',
    variants: [{ id: 3, size: 'M', color: 'AZUL', imageUrl: 'blue.jpg', isActive: true, availableStock: 2 }],
  };
  assert.equal(imageForColor(product, 'AZUL'), 'blue.jpg');
  assert.equal(imageForColor(product, 'NEGRO'), null);
  assert.equal(reconcileCart([{ id: 1, variantId: 3, quantity: 1 }], [product])[0].imageUrl, 'blue.jpg');
});
test('todos los artículos demo tienen tres tallas y cuatro colores sin IDs duplicados', () => {
  const ids = new Set();
  for (const product of demoProducts) {
    assert.equal(product.variants.length, 12);
    assert.equal(new Set(product.variants.map((v) => v.color)).size, 4);
    for (const variant of product.variants) {
      assert.ok(!ids.has(variant.id));
      ids.add(variant.id);
    }
  }
});
