import test from 'node:test';
import assert from 'node:assert/strict';
import { featuredProducts, claimView, readCounts, VIEW_WINDOW_MS } from './productViews.js';

test('destacados: activos, ocho como máximo y desempate estable sin mutar catálogo', () => {
  const products = Array.from({ length: 12 }, (_, i) => ({
    id: i + 1,
    isActive: i !== 11,
    viewCount: i === 10 ? 20 : 0,
  }));
  products[11].viewCount = 100;
  assert.deepEqual(
    featuredProducts(products).map((p) => p.id),
    [11, 1, 2, 3, 4, 5, 6, 7],
  );
  assert.equal(products[0].id, 1);
});

test('una visita por producto y ventana de 30 minutos en la sesión', () => {
  const data = new Map();
  const storage = { getItem: (key) => data.get(key) ?? null, setItem: (key, value) => data.set(key, value) };
  assert.equal(claimView(storage, '1', 1000), true);
  assert.equal(claimView(storage, '1', 2000), false);
  assert.equal(claimView(storage, '2', 2000), true);
  assert.equal(claimView(storage, '1', 1000 + VIEW_WINDOW_MS), true);
  assert.deepEqual(readCounts({ getItem: () => 'invalid' }, 'counts'), {});
});

test('el conteo no bloquea el catálogo cuando storage falla o contiene datos corruptos', () => {
  const blocked = {
    getItem() {
      throw new Error('Storage bloqueado');
    },
  };
  assert.equal(claimView(blocked, 'producto', 1000), true);
  assert.deepEqual(readCounts(blocked, 'counts'), {});
  for (const value of ['null', '[]', '42', '"texto"']) {
    assert.deepEqual(readCounts({ getItem: () => value }, 'counts'), {});
  }
});
