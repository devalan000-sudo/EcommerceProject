import test from 'node:test';
import assert from 'node:assert/strict';
import { generateWhatsAppLink, generateWhatsAppMessage } from './whatsapp.js';
import { cartTotal } from './money.js';

test('incluye colores y marca la simulación en el texto codificado', () => {
  const items = [{ name: 'Playera Essentials', price: 300, quantity: 2, details: 'una color rojo y otra negra' }];
  const url = new URL(generateWhatsAppLink(items, 600, '525512345678', true));
  const message = url.searchParams.get('text');
  assert.match(message, /^\[SIMULACIÓN/);
  assert.ok(message.includes('2x Playera Essentials: una color rojo y otra negra'));
  assert.ok(message.includes('Total de productos: $600.00 MXN'));
});

test('codifica el pedido, cantidades, MXN y caracteres especiales', () => {
  const items = [{ name: 'Tenis & café', price: 1000, quantity: 2 }];
  const url = new URL(generateWhatsAppLink(items, 2000, '+52 (55) 1234-5678'));
  assert.equal(url.origin + url.pathname, 'https://wa.me/525512345678');
  assert.equal(url.searchParams.get('text'), 'Hola, me interesa comprar:\n- 2x Tenis & café ($1,000.00 c/u)\nTotal de productos: $2,000.00 MXN\n¿Me confirman disponibilidad y costo de envío?');
});
test('suma dinero sin residuos de coma flotante', () => {
  assert.equal(cartTotal([{ price: 0.1, quantity: 1 }, { price: 0.2, quantity: 1 }]), 0.3);
});
test('rechaza carritos vacíos, teléfonos inválidos y totales inconsistentes', () => {
  const items = [{ name: 'Producto', price: 10, quantity: 2 }];
  assert.throws(() => generateWhatsAppLink([], 0, '525512345678'));
  assert.throws(() => generateWhatsAppLink(items, 20, 'abc'));
  assert.throws(() => generateWhatsAppLink(items, 10, '525512345678'));
  assert.throws(() => generateWhatsAppLink([{ ...items[0], quantity: -1 }], -10, '525512345678'));
});

test('la vista previa coincide con el texto del enlace y no altera el carrito', () => {
  const items = Object.freeze([
    Object.freeze({ name: 'Playera\nEssential', price: 349.50, quantity: 2, details: 'roja & negra\ntalla M' }),
    Object.freeze({ name: 'Gorra', price: 100, quantity: 1 }),
  ]);
  const total = cartTotal(items);
  const message = generateWhatsAppMessage(items, total);
  const url = new URL(generateWhatsAppLink(items, total, '525512345678'));
  assert.equal(url.searchParams.get('text'), message);
  assert.ok(message.includes('Playera Essential: roja & negra talla M'));
  assert.ok(message.includes('Total de productos: $799.00 MXN'));
  assert.equal(message.includes('SIMULACIÓN'), false);
  assert.equal(items[0].name, 'Playera\nEssential');
});

for (const [label, overrides] of [
  ['cantidad fraccionaria', { quantity: 1.5 }],
  ['cantidad cero', { quantity: 0 }],
  ['precio cero', { price: 0 }],
  ['precio no finito', { price: Infinity }],
  ['precio como texto', { price: '100' }],
  ['nombre inválido', { name: null }],
]) {
  test(`WhatsApp rechaza ${label}`, () => {
    assert.throws(() => generateWhatsAppMessage([{ name: 'Producto', price: 100, quantity: 1, ...overrides }], 100), /producto inválido/);
  });
}

test('limita detalles largos y descarta detalles que no sean texto', () => {
  const item = { name: 'Playera', price: 100, quantity: 1, details: 'x'.repeat(201) };
  const message = generateWhatsAppMessage([item], 100);
  assert.ok(message.includes('x'.repeat(200)));
  assert.equal(message.includes('x'.repeat(201)), false);
  assert.equal(generateWhatsAppMessage([{ ...item, details: { color: 'rojo' } }], 100).includes('[object Object]'), false);
});
